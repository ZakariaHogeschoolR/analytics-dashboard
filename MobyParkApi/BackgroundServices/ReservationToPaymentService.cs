using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Models;

namespace MobyParkApi.BackgroundServices;

public class ReservationToPaymentService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationToPaymentService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public ReservationToPaymentService(
        IServiceProvider serviceProvider,
        ILogger<ReservationToPaymentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationToPaymentService gestart - controleert elke {Interval} seconden", 
            _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredReservations();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij het verwerken van verlopen reserveringen");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredReservations()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var now = GetCurrentTimestampWithoutTimezone();
        
        // Haal alleen de velden op die we nodig hebben, zonder discount_code_id
        var expiredReservations = await dbContext.Reservations
            .Where(r => r.EndTime != null && r.EndTime < now)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                r.ParkingLotId,
                r.VehicleId,
                r.StartTime,
                r.EndTime,
                r.Cost,
                LicensePlate = r.Vehicle != null ? r.Vehicle.LicensePlate : "UNKNOWN"
            })
            .ToListAsync();

        if (!expiredReservations.Any())
        {
            _logger.LogInformation("Geen verlopen reserveringen gevonden");
            return;
        }

        _logger.LogInformation("Gevonden: {Count} verlopen reserveringen, controleren op bestaande payments...", 
            expiredReservations.Count);

        int createdCount = 0;

        foreach (var reservation in expiredReservations)
        {
            try
            {
                // Check of er al een Payment bestaat
                bool paymentExists = await dbContext.Payments
                    .AnyAsync(p => 
                        p.ParkingLotId == reservation.ParkingLotId &&
                        p.StartTime == reservation.StartTime &&
                        p.EndTime == reservation.EndTime &&
                        (p.UserId == reservation.UserId || 
                         p.LicensePlate == reservation.LicensePlate));

                if (paymentExists)
                {
                    _logger.LogInformation("Payment bestaat al voor Reservation {ReservationId}, skip", 
                        reservation.Id);
                    continue;
                }

                // Maak nieuwe Payment
                var payment = new Payments
                {
                    UserId = reservation.UserId,
                    ParkingLotId = reservation.ParkingLotId,
                    LicensePlate = reservation.LicensePlate,
                    StartTime = reservation.StartTime,
                    EndTime = reservation.EndTime ?? now,
                    Duration = CalculateDuration(reservation.StartTime, reservation.EndTime ?? now),
                    Cost = reservation.Cost,
                    Discount = 0m,
                    PaymentStatus = "Pending",
                    CreatedAt = now,
                    ModifiedAt = now
                    // DiscountCodeId wordt niet gezet (kolom bestaat wel in payments tabel)
                };

                dbContext.Payments.Add(payment);
                createdCount++;
                
                _logger.LogInformation(
                    "Payment aangemaakt voor Reservation {ReservationId}, bedrag: {Cost:C}, status: {Status}", 
                    reservation.Id, payment.Cost, payment.PaymentStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij het aanmaken van payment voor Reservation {ReservationId}", 
                    reservation.Id);
            }
        }

        if (createdCount > 0)
        {
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Succesvol {Count} nieuwe payments opgeslagen", createdCount);
        }
        else
        {
            _logger.LogInformation("Geen nieuwe payments aangemaakt");
        }
    }

    private int CalculateDuration(DateTime startTime, DateTime endTime)
    {
        return (int)(endTime - startTime).TotalMinutes;
    }

    private static DateTime GetCurrentTimestampWithoutTimezone()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }
}