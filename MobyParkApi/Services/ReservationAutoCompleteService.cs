namespace MobyParkApi.Services;

using Microsoft.EntityFrameworkCore;
using MobyParkApi.Models;
using MobyParkApi.Data;

public interface IReservationAutoCompleteService
{
    /// <summary>
    /// Controleert en update een reservation als deze verlopen is
    /// Maakt automatisch een payment aan als status wordt geüpdatet naar confirmed
    /// </summary>
    Task<bool> CheckAndCompleteReservationAsync(Reservations reservation);

    /// <summary>
    /// Controleert en update alle verlopen reservations
    /// </summary>
    Task<int> CheckAndCompleteAllExpiredReservationsAsync();
}

public class ReservationAutoCompleteService : IReservationAutoCompleteService
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentGenerationService _paymentGenerationService;
    private readonly ILogger<ReservationAutoCompleteService> _logger;

    public ReservationAutoCompleteService(
        ApplicationDbContext context,
        IPaymentGenerationService paymentGenerationService,
        ILogger<ReservationAutoCompleteService> logger)
    {
        _context = context;
        _paymentGenerationService = paymentGenerationService;
        _logger = logger;
    }

    public async Task<bool> CheckAndCompleteReservationAsync(Reservations reservation)
    {
        // Alleen pending reservations kunnen auto-completed worden (case-insensitive check)
        if (!string.Equals(reservation.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check of eindtijd is verstreken
        if (!reservation.EndTime.HasValue)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var endTimeUtc = reservation.EndTime.Value.Kind == DateTimeKind.Utc
            ? reservation.EndTime.Value
            : DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Utc);

        if (endTimeUtc > now)
        {
            // Nog niet verlopen
            return false;
        }

        _logger.LogInformation("Auto-completing expired reservation {ReservationId}", reservation.Id);

        // Update status naar Confirmed (met hoofdletter - consistent met database)
        reservation.Status = "Confirmed";
        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} status updated to Confirmed", reservation.Id);

        // Maak automatisch een payment aan
        try
        {
            var paymentId = await _paymentGenerationService.CreatePaymentFromReservationAsync(reservation.Id);
            _logger.LogInformation("Auto-created payment {PaymentId} for reservation {ReservationId}", 
                paymentId, reservation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-create payment for reservation {ReservationId}", reservation.Id);
            // Continue - reservation is al confirmed, payment kan later nog worden aangemaakt
        }

        return true;
    }

    public async Task<int> CheckAndCompleteAllExpiredReservationsAsync()
    {
        _logger.LogInformation("Checking for expired reservations");

        var now = DateTime.UtcNow;

        // Haal alle pending reservations op met verlopen eindtijd (case-insensitive)
        var expiredReservations = await _context.Reservations
            .Where(r => EF.Functions.ILike(r.Status, "pending") && 
                       r.EndTime.HasValue && 
                       r.EndTime.Value <= now)
            .ToListAsync();

        _logger.LogInformation("Found {Count} expired reservations", expiredReservations.Count);

        int completedCount = 0;

        foreach (var reservation in expiredReservations)
        {
            try
            {
                var wasCompleted = await CheckAndCompleteReservationAsync(reservation);
                if (wasCompleted)
                {
                    completedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing reservation {ReservationId}", reservation.Id);
            }
        }

        _logger.LogInformation("Auto-completed {Count} reservations", completedCount);
        return completedCount;
    }
}