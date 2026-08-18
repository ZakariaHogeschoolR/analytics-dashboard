using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MobyParkApi.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReservationService> _logger;
        private readonly IArchiveService _archiveService;
        private readonly IDiscountCodeService _discountCodeService;

        public ReservationService(
            ApplicationDbContext context, 
            ILogger<ReservationService> logger, 
            IArchiveService archiveService,
            IDiscountCodeService discountCodeService)
        {
            _context = context;
            _logger = logger;
            _archiveService = archiveService;
            _discountCodeService = discountCodeService;
        }

        // Helper method voor case-insensitive admin check
        private bool IsAdmin(string userRole)
        {
            return string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ReservationResponseDto?> GetReservationByIdAsync(int id, int currentUserId, string userRole)
        {
            _logger.LogDebug("Ophalen reservering {ReservationId} voor user {UserId} met rol {Role}", 
                id, currentUserId, userRole);

            var reservation = await _context.Reservations
                .Include(r => r.ParkingLot)
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                _logger.LogWarning("Reservering {ReservationId} niet gevonden", id);
                return null;
            }

            // Check authorization - case-insensitive admin check
            if (!IsAdmin(userRole) && reservation.UserId != currentUserId)
            {
                _logger.LogWarning("User {UserId} heeft geen toegang tot reservering {ReservationId} (eigenaar: {OwnerId})", 
                    currentUserId, id, reservation.UserId);
                throw new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering");
            }

            return reservation.ToResponseDto();
        }


        public async Task<ReservationResponseDto> CreateReservationAsync(ReservationDto dto, int currentUserId)
        {
            _logger.LogInformation("Start aanmaken reservering voor user {UserId}, parkeerplaats {ParkingLotId}, periode {Start} tot {End}", 
                currentUserId, dto.ParkingLotId, dto.StartDate, dto.EndDate);

            try
            {
                // Check parking lot
                var parkingLot = await _context.ParkingLots.FindAsync(dto.ParkingLotId);
                if (parkingLot == null)
                {
                    _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden", dto.ParkingLotId);
                    throw new KeyNotFoundException("Parkeerplaats niet gevonden");
                }

                _logger.LogDebug("Parkeerplaats {ParkingLotId} gevonden: {Name}, tarief: {Tariff}", 
                    parkingLot.Id, parkingLot.Name, parkingLot.Tariff);

                // Check vehicle via licensePlate en controleer eigendom
                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == dto.LicensePlate);

                if (vehicle == null)
                {
                    _logger.LogWarning("Voertuig met kenteken {LicensePlate} niet gevonden", dto.LicensePlate);
                    throw new ArgumentException("Kenteken niet gevonden of niet van jou");
                }

                if (vehicle.UserId != currentUserId)
                {
                    _logger.LogWarning("Voertuig {VehicleId} met kenteken {LicensePlate} is niet van user {UserId} maar van {OwnerId}", 
                        vehicle.Id, dto.LicensePlate, currentUserId, vehicle.UserId);
                    throw new ArgumentException("Kenteken niet gevonden of niet van jou");
                }

                // Parse datums
                var startTime = ParseDateTime(dto.StartDate);
                var endTime = ParseDateTime(dto.EndDate);

                if (startTime == null || endTime == null)
                {
                    _logger.LogWarning("Ongeldige datum formaat: StartDate={StartDate}, EndDate={EndDate}", 
                        dto.StartDate, dto.EndDate);
                    throw new ArgumentException("Ongeldig datum formaat. Gebruik YYYY-MM-DD HH:MM:SS");
                }

                // TIJD VALIDATIES
                if (startTime.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Starttijd {StartTime} ligt in het verleden", startTime.Value);
                    throw new ArgumentException("Starttijd moet in de toekomst zijn");
                }

                if (endTime.Value <= startTime.Value)
                {
                    _logger.LogWarning("Eindtijd {EndTime} is niet na starttijd {StartTime}", 
                        endTime.Value, startTime.Value);
                    throw new ArgumentException("Eindtijd moet na starttijd zijn");
                }

                // CHECK BESCHIKBAARHEID
                var (isAvailable, availableSpots) = await CheckAvailability(dto.ParkingLotId, startTime.Value, endTime.Value);
                
                if (!isAvailable)
                {
                    _logger.LogWarning("Geen beschikbaarheid voor parkeerplaats {ParkingLotId} van {StartTime} tot {EndTime}. Beschikbare plekken: {AvailableSpots}", 
                        dto.ParkingLotId, startTime.Value, endTime.Value, availableSpots);
                    throw new ArgumentException("Geen beschikbare plekken in deze periode");
                }

                _logger.LogInformation("Beschikbaarheid check succesvol: {AvailableSpots} plekken beschikbaar op parkeerplaats {ParkingLotId}", 
                    availableSpots, dto.ParkingLotId);

                // BEREKEN KOSTEN
                var durationHours = (decimal)(endTime.Value - startTime.Value).TotalHours;
                var calculatedCost = decimal.Round(durationHours * Convert.ToDecimal(parkingLot.Tariff), 2, MidpointRounding.AwayFromZero);

                _logger.LogDebug("Kosten berekend: {Hours} uur × €{Tariff} = €{Cost}", 
                    durationHours, parkingLot.Tariff, calculatedCost);

                // APPLY DISCOUNT CODE IF PROVIDED
                decimal discountAmount = 0;
                int? discountCodeId = null;
                
                if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
                {
                    try
                    {
                        discountAmount = await _discountCodeService.ApplyDiscountCodeAsync(
                            dto.DiscountCode,
                            currentUserId,
                            dto.ParkingLotId,
                            startTime.Value,
                            calculatedCost,
                            reservationId: null, // Will be set after reservation is created
                            paymentId: null
                        );

                        var discountCode = await _context.DiscountCodes
                            .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == dto.DiscountCode.ToUpper());
                        
                        if (discountCode != null)
                        {
                            discountCodeId = discountCode.Id;
                        }

                        calculatedCost = Math.Max(0, calculatedCost - discountAmount);
                        _logger.LogInformation("Kortingscode {Code} toegepast: €{DiscountAmount} korting, nieuwe kosten: €{Cost}",
                            dto.DiscountCode, discountAmount, calculatedCost);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fout bij toepassen kortingscode {Code}: {Message}", dto.DiscountCode, ex.Message);
                        throw new ArgumentException($"Kortingscode is niet geldig: {ex.Message}");
                    }
                }

                // Create reservation
                var reservation = new Reservations
                {
                    UserId = currentUserId,
                    ParkingLotId = dto.ParkingLotId,
                    VehicleId = vehicle.Id,
                    StartTime = startTime.Value,
                    EndTime = endTime.Value,
                    Status = "Pending",
                    Cost = calculatedCost,
                    DiscountCodeId = discountCodeId,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = null
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                // Update discount code usage with reservation ID if discount was applied
                if (discountCodeId.HasValue && discountAmount > 0)
                {
                    var usage = await _context.DiscountCodeUsage
                        .Where(u => u.DiscountCodeId == discountCodeId.Value && u.ReservationId == null)
                        .OrderByDescending(u => u.UsedAt)
                        .FirstOrDefaultAsync();
                    
                    if (usage != null)
                    {
                        usage.ReservationId = reservation.Id;
                        await _context.SaveChangesAsync();
                    }
                }

                _logger.LogInformation("Reservering {ReservationId} aangemaakt voor user {UserId}, parkeerplaats {ParkingLotId}, voertuig {VehicleId}, kosten €{Cost}, korting €{Discount}", 
                    reservation.Id, currentUserId, dto.ParkingLotId, vehicle.Id, calculatedCost, discountAmount);

                // Reload met includes voor response DTO
                var createdReservation = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .Include(r => r.Vehicle)
                    .FirstAsync(r => r.Id == reservation.Id);

                return createdReservation.ToResponseDto();
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Onverwachte fout bij aanmaken reservering voor user {UserId}", currentUserId);
                throw;
            }
        }

        private async Task<(bool isAvailable, int availableSpots)> CheckAvailability(
            int parkingLotId,
            DateTime startTime,
            DateTime endTime)
        {
            _logger.LogDebug("Check beschikbaarheid voor parkeerplaats {ParkingLotId} van {StartTime} tot {EndTime}", 
                parkingLotId, startTime, endTime);

            // Haal parkeerplaats op met capaciteit
            var parkingLot = await _context.ParkingLots.FindAsync(parkingLotId);
            if (parkingLot == null)
            {
                _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden bij beschikbaarheid check", parkingLotId);
                return (false, 0);
            }

            // Tel overlappende reserveringen
            var overlappingReservations = await _context.Reservations
                .Where(r => r.ParkingLotId == parkingLotId)
                .Where(r => r.Status != "Cancelled")
                .Where(r => r.StartTime < endTime && r.EndTime > startTime)
                .CountAsync();

            // Bereken beschikbare plekken
            int availableSpots = parkingLot.Capacity - overlappingReservations;
            bool isAvailable = availableSpots > 0;

            _logger.LogDebug("Beschikbaarheid resultaat voor {ParkingLotId}: Capaciteit={Capacity}, Overlappend={Overlapping}, Beschikbaar={Available}", 
                parkingLotId, parkingLot.Capacity, overlappingReservations, availableSpots);

            return (isAvailable, availableSpots);
        }

        public async Task<ReservationResponseDto> UpdateReservationAsync(int id, ReservationDto dto, int currentUserId, string userRole)
        {
            _logger.LogInformation("Start updaten reservering {ReservationId} voor user {UserId}", id, currentUserId);

            try
            {
                var reservation = await _context.Reservations.FindAsync(id);
                if (reservation == null)
                {
                    _logger.LogWarning("Reservering {ReservationId} niet gevonden voor update", id);
                    throw new KeyNotFoundException("Reservering niet gevonden");
                }

                // Check authorization - case-insensitive admin check
                if (!IsAdmin(userRole) && reservation.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} heeft geen toegang tot reservering {ReservationId} voor update", 
                        currentUserId, id);
                    throw new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering");
                }

                // Check parking lot
                var newParkingLot = await _context.ParkingLots.FindAsync(dto.ParkingLotId);
                if (newParkingLot == null)
                {
                    _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden voor update", dto.ParkingLotId);
                    throw new KeyNotFoundException("Parkeerplaats niet gevonden");
                }

                // Check vehicle via licensePlate en controleer eigendom
                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == dto.LicensePlate);

                if (vehicle == null || vehicle.UserId != currentUserId)
                {
                    _logger.LogWarning("Voertuig met kenteken {LicensePlate} niet gevonden of niet van user {UserId}", 
                        dto.LicensePlate, currentUserId);
                    throw new ArgumentException("Kenteken niet gevonden of niet van jou");
                }

                // Parse datums
                var startTime = ParseDateTime(dto.StartDate);
                var endTime = ParseDateTime(dto.EndDate);

                if (startTime == null || endTime == null)
                {
                    _logger.LogWarning("Ongeldige datum formaat bij update: StartDate={StartDate}, EndDate={EndDate}", 
                        dto.StartDate, dto.EndDate);
                    throw new ArgumentException("Ongeldig datum formaat. Gebruik YYYY-MM-DD HH:MM:SS");
                }

                // TIJD VALIDATIES
                if (startTime.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Starttijd {StartTime} ligt in het verleden bij update", startTime.Value);
                    throw new ArgumentException("Starttijd moet in de toekomst zijn");
                }

                if (endTime.Value <= startTime.Value)
                {
                    _logger.LogWarning("Eindtijd {EndTime} is niet na starttijd {StartTime} bij update", 
                        endTime.Value, startTime.Value);
                    throw new ArgumentException("Eindtijd moet na starttijd zijn");
                }

                // CHECK BESCHIKBAARHEID (sluit huidige reservering uit)
                var (isAvailable, availableSpots) = await CheckAvailability(
                    dto.ParkingLotId,
                    startTime.Value,
                    endTime.Value,
                    excludeReservationId: id);

                if (!isAvailable)
                {
                    _logger.LogWarning("Geen beschikbaarheid voor parkeerplaats {ParkingLotId} van {StartTime} tot {EndTime} bij update reservering {ReservationId}", 
                        dto.ParkingLotId, startTime.Value, endTime.Value, id);
                    throw new ArgumentException("Geen beschikbare plekken in deze periode");
                }

                // BEREKEN KOSTEN
                var durationHours = (decimal)(endTime.Value - startTime.Value).TotalHours;
                var calculatedCost = decimal.Round(durationHours * Convert.ToDecimal(newParkingLot.Tariff), 2, MidpointRounding.AwayFromZero);

                _logger.LogDebug("Update reservering {ReservationId}: Oude kosten €{OldCost}, nieuwe kosten €{NewCost}", 
                    id, reservation.Cost, calculatedCost);

                // Update reservation
                reservation.ParkingLotId = dto.ParkingLotId;
                reservation.VehicleId = vehicle.Id;
                reservation.StartTime = startTime.Value;
                reservation.EndTime = endTime.Value;
                reservation.Status = "Confirmed";
                reservation.Cost = calculatedCost;
                reservation.ModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Reservering {ReservationId} succesvol geüpdatet voor user {UserId}", id, currentUserId);

                // Reload met includes voor response DTO
                var updatedReservation = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .Include(r => r.Vehicle)
                    .FirstAsync(r => r.Id == id);

                return updatedReservation.ToResponseDto();
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Onverwachte fout bij updaten reservering {ReservationId}", id);
                throw;
            }
        }
        
        public async Task<List<ReservationResponseDto>> GetAllUserReservationsAsync(int currentUserId)
        {
            _logger.LogDebug("Ophalen alle reserveringen voor user {UserId}", currentUserId);

            var now = DateTime.UtcNow;
            // Filter: alleen actieve reserveringen (EndTime >= NOW() OR EndTime IS NULL)
            var reservations = await _context.Reservations
                .Where(r => r.UserId == currentUserId)
                .Where(r => r.EndTime == null || r.EndTime >= now)
                .Include(r => r.ParkingLot)
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.StartTime)
                .ToListAsync();

            _logger.LogDebug("Gevonden {Count} actieve reserveringen voor user {UserId}", reservations.Count, currentUserId);

            return reservations.ToResponseDtoList();
        }

        private async Task<(bool isAvailable, int availableSpots)> CheckAvailability(
            int parkingLotId,
            DateTime startTime,
            DateTime endTime,
            int? excludeReservationId = null)
        {
            _logger.LogDebug("Check beschikbaarheid voor parkeerplaats {ParkingLotId} (exclusief reservering {ExcludeId})", 
                parkingLotId, excludeReservationId);

            // Haal parkeerplaats op met capaciteit
            var parkingLot = await _context.ParkingLots.FindAsync(parkingLotId);
            if (parkingLot == null)
            {
                _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden", parkingLotId);
                return (false, 0);
            }

            // Tel overlappende reserveringen (sluit huidige reservering uit bij update)
            var query = _context.Reservations
                .Where(r => r.ParkingLotId == parkingLotId)
                .Where(r => r.Status != "Cancelled")
                .Where(r => r.StartTime < endTime && r.EndTime > startTime);

            // Sluit de huidige reservering uit als we aan het updaten zijn
            if (excludeReservationId.HasValue)
            {
                query = query.Where(r => r.Id != excludeReservationId.Value);
            }

            var overlappingReservations = await query.CountAsync();

            // Bereken beschikbare plekken
            int availableSpots = parkingLot.Capacity - overlappingReservations;
            bool isAvailable = availableSpots > 0;

            _logger.LogDebug("Beschikbaarheid: Capaciteit={Capacity}, Overlappend={Overlapping}, Beschikbaar={Available}", 
                parkingLot.Capacity, overlappingReservations, availableSpots);

            return (isAvailable, availableSpots);
        }

        private async Task<bool> CheckVehicleAvailability(
            int vehicleId,
            DateTime startTime,
            DateTime endTime,
            int? excludeReservationId = null)
        {
            _logger.LogDebug("Check voertuig beschikbaarheid voor voertuig {VehicleId} van {StartTime} tot {EndTime} (exclusief reservering {ExcludeId})",
                vehicleId, startTime, endTime, excludeReservationId);

            var query = _context.Reservations
                .Where(r => r.VehicleId == vehicleId)
                .Where(r => r.Status != "Cancelled")
                .Where(r => r.StartTime < endTime && r.EndTime > startTime);

            if (excludeReservationId.HasValue)
            {
                query = query.Where(r => r.Id != excludeReservationId.Value);
            }

            var hasOverlap = await query.AnyAsync();

            _logger.LogDebug("Voertuig {VehicleId} beschikbaar: {IsAvailable}", vehicleId, !hasOverlap);

            return !hasOverlap;
        }

        public async Task<ReservationResponseDto> UpdateReservationTimeAsync(int id, UpdateReservationTimeDto dto, int currentUserId, string userRole)
        {
            _logger.LogInformation("Start updaten tijdsperiode reservering {ReservationId} voor user {UserId}", id, currentUserId);

            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservering {ReservationId} niet gevonden voor tijd update", id);
                    throw new KeyNotFoundException("Reservering niet gevonden");
                }

                if (!IsAdmin(userRole) && reservation.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} heeft geen toegang tot reservering {ReservationId} voor tijd update",
                        currentUserId, id);
                    throw new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering");
                }

                var startTime = ParseDateTime(dto.StartDate);
                var endTime = ParseDateTime(dto.EndDate);

                if (startTime == null || endTime == null)
                {
                    _logger.LogWarning("Ongeldige datum formaat bij tijd update: StartDate={StartDate}, EndDate={EndDate}",
                        dto.StartDate, dto.EndDate);
                    throw new ArgumentException("Ongeldig datum formaat. Gebruik YYYY-MM-DD HH:MM:SS");
                }

                if (startTime.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Starttijd {StartTime} ligt in het verleden bij tijd update", startTime.Value);
                    throw new ArgumentException("Starttijd moet in de toekomst zijn");
                }

                if (endTime.Value <= startTime.Value)
                {
                    _logger.LogWarning("Eindtijd {EndTime} is niet na starttijd {StartTime} bij tijd update",
                        endTime.Value, startTime.Value);
                    throw new ArgumentException("Eindtijd moet na starttijd zijn");
                }

                var (isAvailable, _) = await CheckAvailability(
                    reservation.ParkingLotId,
                    startTime.Value,
                    endTime.Value,
                    excludeReservationId: id);

                if (!isAvailable)
                {
                    _logger.LogWarning("Geen beschikbaarheid bij tijd update reservering {ReservationId}", id);
                    throw new ArgumentException("Geen beschikbare plekken in deze periode");
                }

                var durationHours = (decimal)(endTime.Value - startTime.Value).TotalHours;
                var calculatedCost = decimal.Round(durationHours * Convert.ToDecimal(reservation.ParkingLot!.Tariff), 2, MidpointRounding.AwayFromZero);

                reservation.StartTime = startTime.Value;
                reservation.EndTime = endTime.Value;
                reservation.Cost = calculatedCost;
                reservation.ModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var updated = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .Include(r => r.Vehicle)
                    .FirstAsync(r => r.Id == id);

                return updated.ToResponseDto();
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Onverwachte fout bij updaten tijdsperiode reservering {ReservationId}", id);
                throw;
            }
        }

        public async Task<ReservationResponseDto> UpdateReservationVehicleAsync(int id, UpdateReservationVehicleDto dto, int currentUserId, string userRole)
        {
            _logger.LogInformation("Start updaten voertuig reservering {ReservationId} voor user {UserId}", id, currentUserId);

            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservering {ReservationId} niet gevonden voor voertuig update", id);
                    throw new KeyNotFoundException("Reservering niet gevonden");
                }

                if (!IsAdmin(userRole) && reservation.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} heeft geen toegang tot reservering {ReservationId} voor voertuig update",
                        currentUserId, id);
                    throw new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering");
                }

                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == dto.LicensePlate);

                if (vehicle == null || vehicle.UserId != currentUserId)
                {
                    _logger.LogWarning("Voertuig {LicensePlate} niet gevonden of niet van user {UserId}",
                        dto.LicensePlate, currentUserId);
                    throw new ArgumentException("Kenteken niet gevonden of niet van jou");
                }

                var isAvailable = await CheckVehicleAvailability(
                    vehicle.Id,
                    reservation.StartTime,
                    reservation.EndTime ?? reservation.StartTime.AddHours(1), // Fallback voor NULL
                    excludeReservationId: id);

                if (!isAvailable)
                {
                    _logger.LogWarning("Voertuig {VehicleId} niet beschikbaar voor reservering {ReservationId}", vehicle.Id, id);
                    throw new ArgumentException("Het geselecteerde voertuig is niet beschikbaar in deze periode");
                }

                var endTime = reservation.EndTime ?? reservation.StartTime.AddHours(1); // Fallback voor NULL
                var durationHours = (decimal)(endTime - reservation.StartTime).TotalHours;
                var calculatedCost = decimal.Round(durationHours * reservation.ParkingLot!.Tariff, 2, MidpointRounding.AwayFromZero);

                reservation.VehicleId = vehicle.Id;
                reservation.Cost = calculatedCost;
                reservation.ModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var updated = await _context.Reservations
                    .Include(r => r.ParkingLot)
                    .Include(r => r.Vehicle)
                    .FirstAsync(r => r.Id == id);

                return updated.ToResponseDto();
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Onverwachte fout bij updaten voertuig reservering {ReservationId}", id);
                throw;
            }
        }

        public async Task DeleteReservationAsync(int id, int currentUserId, string userRole, string username)
        {
            _logger.LogInformation("Start verwijderen reservering {ReservationId} door user {UserId} ({Username})", id, currentUserId, username);

            try
            {
                var reservation = await _context.Reservations.FindAsync(id);
                if (reservation == null)
                {
                    _logger.LogWarning("Reservering {ReservationId} niet gevonden voor verwijdering", id);
                    throw new KeyNotFoundException("Reservering niet gevonden");
                }

                // Check authorization - case-insensitive admin check
                if (!IsAdmin(userRole) && reservation.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} heeft geen toegang tot reservering {ReservationId} voor verwijdering", 
                        currentUserId, id);
                    throw new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering");
                }

                // Check StartTime > NOW() - reservering mag alleen geannuleerd worden als deze nog niet is begonnen
                // Converteer beide naar UTC voor correcte vergelijking
                var startTimeUtc = reservation.StartTime.Kind == DateTimeKind.Utc 
                    ? reservation.StartTime 
                    : DateTime.SpecifyKind(reservation.StartTime, DateTimeKind.Utc);
                
                if (startTimeUtc <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Reservering {ReservationId} kan niet geannuleerd worden, starttijd {StartTime} is al verstreken", id, reservation.StartTime);
                    throw new ArgumentException("Reservering kan niet geannuleerd worden omdat deze al is gestart");
                }

                // Gebruik ArchiveService om de reservering te archiveren
                await _archiveService.ArchiveReservationAsync(reservation, username, "Cancelled");

                _logger.LogInformation("Reservering {ReservationId} succesvol gearchiveerd en verwijderd door user {UserId} ({Username})", 
                    id, currentUserId, username);
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Onverwachte fout bij annuleren reservering {ReservationId}", id);
                throw;
            }
        }

        private DateTime? ParseDateTime(string dateString)
        {
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd"
            };

            if (DateTime.TryParseExact(
                    dateString,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime result))
            {
                return DateTime.SpecifyKind(result, DateTimeKind.Utc);
            }

            return null;
        }
    }
}