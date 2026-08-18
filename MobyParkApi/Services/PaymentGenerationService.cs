namespace MobyParkApi.Services;

using Microsoft.EntityFrameworkCore;
using MobyParkApi.Models;
using MobyParkApi.Data;

public class PaymentGenerationService : IPaymentGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentGenerationService> _logger;

    public PaymentGenerationService(ApplicationDbContext context, ILogger<PaymentGenerationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CreatePaymentFromParkingSessionAsync(int parkingSessionId)
    {
        _logger.LogInformation("Creating payment for parking session {ParkingSessionId}", parkingSessionId);

        // Check of er al een payment bestaat
        var existingPayment = await _context.Payments
            .FirstOrDefaultAsync(p => p.ParkingSessionId == parkingSessionId);

        if (existingPayment != null)
        {
            _logger.LogWarning("Payment already exists for parking session {ParkingSessionId}", parkingSessionId);
            return existingPayment.Id;
        }

        // Haal parking session op
        var session = await _context.ParkingSessions.FindAsync(parkingSessionId);

        if (session == null)
        {
            _logger.LogError("Parking session {ParkingSessionId} not found", parkingSessionId);
            throw new KeyNotFoundException($"Parking session {parkingSessionId} not found");
        }

        // Valideer dat de session is afgerond
        if (session.Stopped == null)
        {
            _logger.LogWarning("Cannot create payment for parking session {ParkingSessionId} - session not yet stopped", parkingSessionId);
            throw new InvalidOperationException("Cannot create payment for active parking session");
        }

        // Zorg dat DateTime objecten UTC zijn voor PostgreSQL
        var startTimeUtc = session.Started.Kind == DateTimeKind.Utc 
            ? session.Started 
            : DateTime.SpecifyKind(session.Started, DateTimeKind.Utc);
        
        var endTimeUtc = session.Stopped.Value.Kind == DateTimeKind.Utc 
            ? session.Stopped.Value 
            : DateTime.SpecifyKind(session.Stopped.Value, DateTimeKind.Utc);

        // Maak payment aan
        var payment = new Payments
        {
            UserId = session.UserId,
            ParkingLotId = session.ParkingLotId,
            ParkingSessionId = parkingSessionId,
            InvoiceId = null, // Wordt later gevuld door invoice generatie
            LicensePlate = session.LicensePlate ?? string.Empty,
            Duration = session.DurationMinutes ?? 0,
            PaymentStatus = "pending",
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            Cost = session.Cost ?? 0,
            Discount = 0.0m,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created payment {PaymentId} for parking session {ParkingSessionId} with cost {Cost}",
            payment.Id, parkingSessionId, payment.Cost);

        return payment.Id;
    }

    public async Task<int> CreatePaymentFromReservationAsync(int reservationId)
    {
        _logger.LogInformation("Creating payment for reservation {ReservationId}", reservationId);

        // Check of er al een payment bestaat voor deze reservation
        // We checken of er een payment is met dezelfde user_id, parking_lot_id en tijden
        var reservation = await _context.Reservations
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogError("Reservation {ReservationId} not found", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        // Converteer StartTime naar UTC voor PostgreSQL (herbruik deze variabele later)
        var startTimeUtc = reservation.StartTime.Kind == DateTimeKind.Utc 
            ? reservation.StartTime 
            : DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Utc);

        // Check of er al een payment bestaat
        var existingPayment = await _context.Payments
            .FirstOrDefaultAsync(p => 
                p.UserId == reservation.UserId &&
                p.ParkingLotId == reservation.ParkingLotId &&
                p.StartTime == startTimeUtc &&
                p.ParkingSessionId == null); // Geen parking session, dus van reservation

        if (existingPayment != null)
        {
            _logger.LogWarning("Payment already exists for reservation {ReservationId}", reservationId);
            return existingPayment.Id;
        }

        // Status validatie verwijderd - payments kunnen voor alle statussen worden aangemaakt
        // Dit is handig omdat de auto-complete service automatisch payments aanmaakt bij status "confirmed"

        // Bereken end time - als end_time null is, gebruik start_time + 1 uur als default
        DateTime endTime = reservation.EndTime ?? reservation.StartTime.AddHours(1);

        // Converteer alleen EndTime naar UTC (StartTimeUtc is al gedeclareerd hierboven)
        var endTimeUtc = endTime.Kind == DateTimeKind.Utc 
            ? endTime 
            : DateTime.SpecifyKind(endTime, DateTimeKind.Utc);

        // Bereken duration in minuten
        var duration = (int)(endTimeUtc - startTimeUtc).TotalMinutes;

        // Haal license plate op van vehicle
        var licensePlate = reservation.Vehicle?.LicensePlate ?? string.Empty;

        // Haal discount code op van reservation als die bestaat
        var discountCodeId = reservation.DiscountCodeId;
        var discountAmount = 0.0m;
        
        if (discountCodeId.HasValue)
        {
            // Bereken discount amount op basis van original cost
            // We moeten de original cost berekenen zonder discount
            // Voor nu gebruiken we de cost van de reservation (die al de discount heeft)
            // In een echte implementatie zou je de original cost moeten opslaan
            var discountCode = await _context.DiscountCodes.FindAsync(discountCodeId.Value);
            if (discountCode != null)
            {
                // Schat de original cost (reservation.Cost is al met discount)
                // Dit is een vereenvoudiging - in productie zou je de original cost moeten opslaan
                var estimatedOriginalCost = reservation.Cost;
                if (discountCode.DiscountType == "Percentage")
                {
                    estimatedOriginalCost = reservation.Cost / (1 - (discountCode.DiscountValue / 100));
                }
                else if (discountCode.DiscountType == "FixedAmount")
                {
                    estimatedOriginalCost = reservation.Cost + discountCode.DiscountValue;
                }
                discountAmount = estimatedOriginalCost - reservation.Cost;
            }
        }

        // Maak payment aan
        var payment = new Payments
        {
            UserId = reservation.UserId,
            ParkingLotId = reservation.ParkingLotId,
            ParkingSessionId = null, // Geen parking session, dit is van een reservation
            InvoiceId = null, // Wordt later gevuld door invoice generatie
            LicensePlate = licensePlate,
            Duration = duration,
            PaymentStatus = "pending",
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            Cost = reservation.Cost,
            Discount = discountAmount,
            DiscountCodeId = discountCodeId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created payment {PaymentId} for reservation {ReservationId} with cost {Cost}",
            payment.Id, reservationId, payment.Cost);

        return payment.Id;
    }

    public async Task<bool> PaymentExistsForParkingSessionAsync(int parkingSessionId)
    {
        return await _context.Payments
            .AnyAsync(p => p.ParkingSessionId == parkingSessionId);
    }

    public async Task<bool> PaymentExistsForReservationAsync(int reservationId)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation == null)
        {
            return false;
        }

        // Check of er een payment bestaat met dezelfde gegevens
        // Converteer StartTime naar UTC voor PostgreSQL vergelijking
        var startTimeUtc = reservation.StartTime.Kind == DateTimeKind.Utc 
            ? reservation.StartTime 
            : DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Utc);
        
        return await _context.Payments
            .AnyAsync(p => 
                p.UserId == reservation.UserId &&
                p.ParkingLotId == reservation.ParkingLotId &&
                p.StartTime == startTimeUtc &&
                p.ParkingSessionId == null);
    }
}