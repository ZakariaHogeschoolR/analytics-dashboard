using MobyParkApi.Data;
using MobyParkApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MobyParkApi.Services;

public class ArchiveService : IArchiveService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(ApplicationDbContext context, ILogger<ArchiveService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> ArchiveReservationsAsync()
    {
        _logger.LogInformation("Start archiveren van afgeronde reserveringen");

        var now = DateTime.UtcNow;

        // Haal alle reserveringen op waar EndTime < NOW() en EndTime IS NOT NULL
        var completedReservations = await _context.Reservations
            .Where(r => r.EndTime.HasValue && r.EndTime.Value < now)
            .ToListAsync();

        if (completedReservations.Count == 0)
        {
            _logger.LogInformation("Geen reserveringen gevonden om te archiveren");
            return 0;
        }

        _logger.LogInformation("Gevonden {Count} reserveringen om te archiveren", completedReservations.Count);

        // Gebruik database transaction voor atomiciteit
        if (!_context.Database.IsInMemory())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var archivedCount = 0;

                foreach (var reservation in completedReservations)
                {
                    // Maak archived reservation record
                    var archivedReservation = new ArchivedReservations
                    {
                        UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                        ParkingLotId = reservation.ParkingLotId,
                        VehicleId = reservation.VehicleId,
                        StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                        EndDateTime = reservation.EndTime.HasValue
                            ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified)
                            : null,
                        Status = reservation.Status,
                        Cost = reservation.Cost,
                        CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                        ModifiedAt = reservation.ModifiedAt.HasValue
                            ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = "System"
                    };

                    _context.ArchivedReservations.Add(archivedReservation);
                    archivedCount++;
                }
            

                // Verwijder de gearchiveerde reserveringen uit de main tabel
                _context.Reservations.RemoveRange(completedReservations);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Succesvol {Count} reserveringen gearchiveerd", archivedCount);
                return archivedCount;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fout bij archiveren van reserveringen. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }
        else
        {
            try
            {
                var archivedCount = 0;

                foreach (var reservation in completedReservations)
                {
                    // Maak archived reservation record
                    var archivedReservation = new ArchivedReservations
                    {
                        UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                        ParkingLotId = reservation.ParkingLotId,
                        VehicleId = reservation.VehicleId,
                        StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                        EndDateTime = reservation.EndTime.HasValue
                            ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified)
                            : null,
                        Status = reservation.Status,
                        Cost = reservation.Cost,
                        CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                        ModifiedAt = reservation.ModifiedAt.HasValue
                            ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = "System"
                    };

                    _context.ArchivedReservations.Add(archivedReservation);
                    archivedCount++;
                }
            

                // Verwijder de gearchiveerde reserveringen uit de main tabel
                _context.Reservations.RemoveRange(completedReservations);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();

                _logger.LogInformation("Succesvol {Count} reserveringen gearchiveerd", archivedCount);
                return archivedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij archiveren van reserveringen. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }


    public async Task<int> ArchiveVehicleAndReservationsAsync(Vehicles vehicle, string archivedBy)
    {
        // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
        if (!_context.Database.IsInMemory())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Haal alle reserveringen op die bij dit voertuig horen
                var reservations = await _context.Reservations
                    .Where(r => r.VehicleId == vehicle.Id)
                    .ToListAsync();

                _logger.LogInformation("Archiveert {Count} reserveringen voor voertuig {VehicleId}", reservations.Count, vehicle.Id);

                // Archiveer alle reserveringen
                foreach (var reservation in reservations)
                {
                    var archivedReservation = new ArchivedReservations
                    {
                        // Id wordt automatisch gegenereerd door de database
                        UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                        ParkingLotId = reservation.ParkingLotId,
                        VehicleId = reservation.VehicleId, // Behoud originele vehicle_id voor referentie
                        StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                        EndDateTime = reservation.EndTime.HasValue 
                            ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified) 
                            : null, // Gebruik computed property die EndDate en EndTime invult
                        Status = reservation.Status,
                        Cost = reservation.Cost,
                        CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                        ModifiedAt = reservation.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified) 
                            : null,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = archivedBy
                    };

                    _context.ArchivedReservations.Add(archivedReservation);
                }

                // Archiveer het voertuig
                var archivedVehicle = new ArchivedVehicles
                {
                    // Id wordt automatisch gegenereerd door de database
                    UserId = vehicle.UserId,
                    LicensePlate = vehicle.LicensePlate,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Color = vehicle.Color,
                    Year = vehicle.Year,
                    CreatedAt = DateTime.SpecifyKind(vehicle.CreatedAt, DateTimeKind.Unspecified),
                    ModifiedAt = vehicle.ModifiedAt.HasValue 
                        ? DateTime.SpecifyKind(vehicle.ModifiedAt.Value, DateTimeKind.Unspecified)
                        : DateTime.SpecifyKind(vehicle.CreatedAt, DateTimeKind.Unspecified),
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedBy
                };

                _context.ArchivedVehicles.Add(archivedVehicle);

                // Verwijder het voertuig (CASCADE verwijdert automatisch de reserveringen, maar die zijn al gearchiveerd)
                _context.Vehicles.Remove(vehicle);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Voertuig {VehicleId} en {ReservationCount} reserveringen succesvol gearchiveerd door {ArchivedBy}", 
                    vehicle.Id, reservations.Count, archivedBy);

                return reservations.Count;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fout bij archiveren en verwijderen van voertuig {VehicleId}", vehicle.Id);
                throw;
            }
        }
        else
        {
            try
            {
                // Haal alle reserveringen op die bij dit voertuig horen
                var reservations = await _context.Reservations
                    .Where(r => r.VehicleId == vehicle.Id)
                    .ToListAsync();

                _logger.LogInformation("Archiveert {Count} reserveringen voor voertuig {VehicleId}", reservations.Count, vehicle.Id);

                // Archiveer alle reserveringen
                foreach (var reservation in reservations)
                {
                    var archivedReservation = new ArchivedReservations
                    {
                        // Id wordt automatisch gegenereerd door de database
                        UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                        ParkingLotId = reservation.ParkingLotId,
                        VehicleId = reservation.VehicleId, // Behoud originele vehicle_id voor referentie
                        StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                        EndDateTime = reservation.EndTime.HasValue 
                            ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified) 
                            : null, // Gebruik computed property die EndDate en EndTime invult
                        Status = reservation.Status,
                        Cost = reservation.Cost,
                        CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                        ModifiedAt = reservation.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified) 
                            : null,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = archivedBy
                    };

                    _context.ArchivedReservations.Add(archivedReservation);
                }

                // Archiveer het voertuig
                var archivedVehicle = new ArchivedVehicles
                {
                    // Id wordt automatisch gegenereerd door de database
                    UserId = vehicle.UserId,
                    LicensePlate = vehicle.LicensePlate,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Color = vehicle.Color,
                    Year = vehicle.Year,
                    CreatedAt = DateTime.SpecifyKind(vehicle.CreatedAt, DateTimeKind.Unspecified),
                    ModifiedAt = vehicle.ModifiedAt.HasValue 
                        ? DateTime.SpecifyKind(vehicle.ModifiedAt.Value, DateTimeKind.Unspecified)
                        : DateTime.SpecifyKind(vehicle.CreatedAt, DateTimeKind.Unspecified),
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedBy
                };

                _context.ArchivedVehicles.Add(archivedVehicle);

                // Verwijder het voertuig (CASCADE verwijdert automatisch de reserveringen, maar die zijn al gearchiveerd)
                _context.Vehicles.Remove(vehicle);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();

                _logger.LogInformation("Voertuig {VehicleId} en {ReservationCount} reserveringen succesvol gearchiveerd door {ArchivedBy}", 
                    vehicle.Id, reservations.Count, archivedBy);

                return reservations.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij archiveren en verwijderen van voertuig {VehicleId}", vehicle.Id);
                throw;
            }
        }
    }

    /// <summary>
    /// Archiveert een enkele reservering
    /// </summary>
    /// <param name="reservation">De reservering die gearchiveerd moet worden</param>
    /// <param name="archivedBy">De gebruikersnaam van degene die de reservering archiveert</param>
    /// <param name="status">De status voor de gearchiveerde reservering (bijv. "Cancelled")</param>
    public async Task ArchiveReservationAsync(Reservations reservation, string role, string archivedBy, string status = "Cancelled")
    {
        // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
        if(role != "Admin")
        {
            throw new UnauthorizedAccessException("Access Denied");
        }
        if (!_context.Database.IsInMemory())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Archiveer de reservering naar archived_reservations
                var archivedReservation = new ArchivedReservations
                {
                    // Id wordt automatisch gegenereerd door de database
                    UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                    ParkingLotId = reservation.ParkingLotId,
                    VehicleId = reservation.VehicleId,
                    StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                    EndDateTime = reservation.EndTime.HasValue 
                        ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified) 
                        : null,
                    Status = status, // Gebruik de opgegeven status (bijv. "Cancelled")
                    Cost = reservation.Cost,
                    CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                    ModifiedAt = reservation.ModifiedAt.HasValue 
                        ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified) 
                        : null,
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedBy
                };

                _context.ArchivedReservations.Add(archivedReservation);

                // Verwijder de reservering uit de main tabel
                _context.Reservations.Remove(reservation);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Reservering {ReservationId} succesvol gearchiveerd en verwijderd door {ArchivedBy}", 
                    reservation.Id, archivedBy);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fout bij archiveren en verwijderen van reservering {ReservationId}", reservation.Id);
                throw;
            }
        }
        else
        {
            try
            {
                // Archiveer de reservering naar archived_reservations
                var archivedReservation = new ArchivedReservations
                {
                    // Id wordt automatisch gegenereerd door de database
                    UserId = reservation.UserId ?? throw new InvalidOperationException($"Reservation {reservation.Id} cannot be archived: UserId is null"),
                    ParkingLotId = reservation.ParkingLotId,
                    VehicleId = reservation.VehicleId,
                    StartTime = DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Unspecified),
                    EndDateTime = reservation.EndTime.HasValue 
                        ? DateTime.SpecifyKind(reservation.EndTime.Value, DateTimeKind.Unspecified) 
                        : null,
                    Status = status, // Gebruik de opgegeven status (bijv. "Cancelled")
                    Cost = reservation.Cost,
                    CreatedAt = DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Unspecified),
                    ModifiedAt = reservation.ModifiedAt.HasValue 
                        ? DateTime.SpecifyKind(reservation.ModifiedAt.Value, DateTimeKind.Unspecified) 
                        : null,
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedBy
                };

                _context.ArchivedReservations.Add(archivedReservation);

                // Verwijder de reservering uit de main tabel
                _context.Reservations.Remove(reservation);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();
                _logger.LogInformation("Reservering {ReservationId} succesvol gearchiveerd en verwijderd door {ArchivedBy}", 
                    reservation.Id, archivedBy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij archiveren en verwijderen van reservering {ReservationId}", reservation.Id);
                throw;
            }
        }
    }

    /// <summary>
    /// Archiveert een payment en verwijdert deze uit de main tabel
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> ArchiveAndDeletePaymentAsync(Payments payment, string role, int archivedByUserId)
    {
        if (payment == null)
            return (false, "payment is leeg");
        
        // Check if role is Admin (case-insensitive)
        if(!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access Denied");
        }

        // payment id ophalen en in de logging voegen
        var originalPaymentId = payment.Id;
        _logger.LogInformation("Start archivering payment {PaymentId}", originalPaymentId);

        // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
        if (!_context.Database.IsInMemory())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // copy paste naar archive
                var archivedPayment = new ArchivedPayments
                {
                    Id = payment.Id,
                    ParkingLotId = payment.ParkingLotId,
                    ParkingSessionId = payment.ParkingSessionId,
                    UserId = payment.UserId ?? throw new InvalidOperationException(
                        $"Payment {payment.Id} cannot be archived: UserId is null"),
                    InvoiceId = payment.InvoiceId,
                    LicensePlate = payment.LicensePlate,
                    Duration = payment.Duration,
                    PaymentStatus = payment.PaymentStatus,
                    StartTime = DateTime.SpecifyKind(payment.StartTime, DateTimeKind.Utc),
                    EndTime = DateTime.SpecifyKind(payment.EndTime, DateTimeKind.Utc),
                    Cost = (double)payment.Cost, // Convert decimal to double
                    Discount = (double)payment.Discount, // Convert decimal to double
                    CreatedAt = DateTime.SpecifyKind(payment.CreatedAt, DateTimeKind.Utc), 
                    ModifiedAt = DateTime.SpecifyKind(payment.ModifiedAt, DateTimeKind.Utc),
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedByUserId
                };

                // voeg toe aan archive
                _context.ArchivedPayments.Add(archivedPayment);
                await _context.SaveChangesAsync();
                
                var archivedCheck = await _context.ArchivedPayments.FirstOrDefaultAsync(ap => ap.Id == originalPaymentId);

                if (archivedCheck == null)
                {
                    await transaction.RollbackAsync();
                    return (false, "Archive payment failed");
                }

                // verwijder uit originele tabel
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                // ✅ Check: Verifieer dat payment verwijderd is
                var deletedCheck = await _context.Payments.FindAsync(originalPaymentId);
                if (deletedCheck != null)
                {
                    return (false, $"Payment {originalPaymentId} bestaat nog steeds na verwijdering");
                }

                _logger.LogInformation("Payment {PaymentId} succesvol verwijderd na archivering", originalPaymentId);
                return (true, "Archivering en verwijdering succesvol");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fout bij archiveren payment {PaymentId}", originalPaymentId);
                return (false, $"Archivering mislukt: {ex.Message}");
            }
        }
        else
        {
            try
            {
                // copy paste naar archive
                var archivedPayment = new ArchivedPayments
                {
                    Id = payment.Id,
                    ParkingLotId = payment.ParkingLotId,
                    ParkingSessionId = payment.ParkingSessionId,
                    UserId = payment.UserId ?? throw new InvalidOperationException(
                        $"Payment {payment.Id} cannot be archived: UserId is null"),
                    InvoiceId = payment.InvoiceId,
                    LicensePlate = payment.LicensePlate,
                    Duration = payment.Duration,
                    PaymentStatus = payment.PaymentStatus,
                    StartTime = DateTime.SpecifyKind(payment.StartTime, DateTimeKind.Utc),
                    EndTime = DateTime.SpecifyKind(payment.EndTime, DateTimeKind.Utc),
                    Cost = (double)payment.Cost, // Convert decimal to double
                    Discount = (double)payment.Discount, // Convert decimal to double
                    CreatedAt = DateTime.SpecifyKind(payment.CreatedAt, DateTimeKind.Utc), 
                    ModifiedAt = DateTime.SpecifyKind(payment.ModifiedAt, DateTimeKind.Utc),
                    ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ArchivedBy = archivedByUserId
                };

                // voeg toe aan archive
                _context.ArchivedPayments.Add(archivedPayment);
                await _context.SaveChangesAsync();
                
                var archivedCheck = await _context.ArchivedPayments.FirstOrDefaultAsync(ap => ap.Id == originalPaymentId);

                if (archivedCheck == null)
                {
                    return (false, "Archive payment failed");
                }

                // verwijder uit originele tabel
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();

                // ✅ Check: Verifieer dat payment verwijderd is
                var deletedCheck = await _context.Payments.FindAsync(originalPaymentId);
                if (deletedCheck != null)
                {
                    return (false, $"Payment {originalPaymentId} bestaat nog steeds na verwijdering");
                }

                _logger.LogInformation("Payment {PaymentId} succesvol verwijderd na archivering", originalPaymentId);
                return (true, "Archivering en verwijdering succesvol");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij archiveren payment {PaymentId}", originalPaymentId);
                return (false, $"Archivering mislukt: {ex.Message}");
            }
        }
    }
}

