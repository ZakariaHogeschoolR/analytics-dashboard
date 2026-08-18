using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Npgsql.Internal;
using System.Text.RegularExpressions;
using MobyParkApi.Service;

namespace MobyParkApi.Service
{
    public class ParkingLotService 
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParkingLotsController> _logger;
        private readonly ILogger<ReservationController> _loggerReservation;
        private readonly ReservationService _reservationService;
        private readonly IAddressValidationService _addressValidationService;

        public ParkingLotService(ApplicationDbContext context, ILogger<ParkingLotsController> logger, ILogger<ReservationController> loggerReservation, ReservationService reservationService, IAddressValidationService addressValidationService)
        {
            _context = context;
            _logger = logger;
            _loggerReservation = loggerReservation;
            _reservationService = reservationService;
            _addressValidationService = addressValidationService;
        }

        public async Task<List<ParkingLots>> GetAllParkingLotsService(
            string sortBy = "name", // name, location, capacity, available
            string order = "asc" // asc, desc
            )
        {
            var query = _context.ParkingLots.AsQueryable();

            query = sortBy.ToLower() switch
            {
                "id" => order == "desc" ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
                "location" => order == "desc" ? query.OrderByDescending(p => p.Location) : query.OrderBy(p => p.Location),
                "capacity" => order == "desc" ? query.OrderByDescending(p => p.Capacity) : query.OrderBy(p => p.Capacity),
                "available" => order == "desc"
                    ? query.OrderByDescending(p => p.Capacity - p.Reserved)
                    : query.OrderBy(p => p.Capacity - p.Reserved),
                _ => order == "desc" ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
            };
            
            var parkingLots = await query.ToListAsync();
            return parkingLots;
        }

        public async Task<ParkingLots?> GetParkingLotByIdService(int id)
        {
            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
            {
                _logger.LogWarning("Parking lot {ParkingLotId} niet gevonden", id);
                throw new KeyNotFoundException();
            }
            if (parkingLot.Capacity < parkingLot.Reserved)
            {
                _logger.LogInformation("Parking lot {ParkingLotId} is vol", id);
                throw new ArgumentException();
            }
            _logger.LogInformation("Parking lot {ParkingLotId} opgehaald", id);
            return parkingLot;
        }

        public async Task<List<ParkingSessionDto>?> GetParkingLotSessionsService(
        int id,
        ClaimsPrincipal user,
        bool activeOnly = false)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException();

            // Check of parking lot bestaat
            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
                throw new KeyNotFoundException();

            // Check of user admin is
            var isAdmin = user.IsInRole("Admin") || user.IsInRole("admin") || user.IsInRole("ADMIN");

            var query = _context.ParkingSessions
                .Where(ps => ps.ParkingLotId == id);

            // ✅ Als NIET admin, alleen eigen sessions tonen
            if (!isAdmin)
                query = query.Where(ps => ps.UserId == userId);

            if (activeOnly)
                query = query.Where(ps => ps.Stopped == null);

            var sessions = await query
                .OrderByDescending(ps => ps.Started)
                .Select(ps => new ParkingSessionDto
                {
                    id = ps.Id,
                    parkingLotId = ps.ParkingLotId,
                    licensePlate = ps.LicensePlate,
                    started = ps.Started,
                    stopped = ps.Stopped ?? DateTime.Now,
                    userId = ps.UserId ?? 0,
                    isWalkUp = ps.IsWalkUp,
                    durationMinutes = ps.DurationMinutes ?? 0,
                    cost = ps.Cost ?? 0,
                    paymentStatus = ps.PaymentStatus,
                    createdAt = ps.CreatedAt,
                    originalSessionId = ps.OriginalSessionId ?? 0
                })
                .ToListAsync();

            _logger.LogInformation(
                "Aantal sessions opgehaald voor parking lot {ParkingLotId} door user {UserId} (Admin: {IsAdmin}): {Count}",
                id, userId, isAdmin, sessions.Count);

            return sessions;
        }

        public async Task<ParkingSessions?> StartSessionService(int id, StartSessionRequestDto request, ClaimsPrincipal user)
        {
            
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(userIdClaim == null)
            {
                if(IsValidDutchLicensePlate(request.LicensePlate))
                {
                    var reservationWalkUpDto = new WalkUpDto {LicensePlate = request.LicensePlate, StartDate = Convert.ToString(DateTime.UtcNow), ParkingLotId = id};
                    var parkingSession = await CreateWalkUpService(reservationWalkUpDto);   
                    return parkingSession;
                }
                else
                {
                    _logger.LogInformation(
                    "LicensePlate Is Wrong {licensePlate}",
                    request.LicensePlate);
                    throw new InvalidOperationException($"LicensePlate Is Wrong {request.LicensePlate}");
                }
            }
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException();

            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
                throw new KeyNotFoundException();
            var activeSession = await _context.ParkingSessions
                .FirstOrDefaultAsync(ps =>
                    ps.ParkingLotId == id &&
                    ps.LicensePlate == request.LicensePlate.ToUpper() &&
                    ps.Stopped == null);

            if (activeSession != null)
                throw new ArgumentException();

            var session = new ParkingSessions
            {
                ParkingLotId = id,
                LicensePlate = request.LicensePlate.ToUpper(),
                Started = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified),
                Stopped = null,
                UserId = userId,
                PaymentStatus = "PENDING",
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.ParkingSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Parking sessie {SessionId} gestart door gebruiker {UserId} voor kenteken {LicensePlate} om {Started}",
                session.Id, userId, request.LicensePlate, session.Started);

            return session;
        }

       
        public async Task<ParkingSessions?> StopSessionService(int id, StopSessionRequestDto request, ClaimsPrincipal user)
        {
            // Normalize license plate to uppercase for consistent comparison
            var normalizedLicensePlate = request.LicensePlate?.ToUpper() ?? string.Empty;
            
            _logger.LogInformation(
                "StopSessionService called: ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}, Normalized={Normalized}",
                id, request.LicensePlate, normalizedLicensePlate);

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(userIdClaim == null)
            {
                if(IsValidDutchLicensePlate(request.LicensePlate))
                {
                    // Check all active sessions for debugging
                    var allActiveSessions = await _context.ParkingSessions
                         .Where(ps => ps.Stopped == null)
                        .ToListAsync();
                 
                    _logger.LogInformation(
                        "Found {Count} active sessions total. Looking for ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                        allActiveSessions.Count, id, normalizedLicensePlate);
                 
                    foreach (var session in allActiveSessions)
                    {
                        _logger.LogInformation(
                            "Active session: Id={Id}, ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                            session.Id, session.ParkingLotId, session.LicensePlate);
                    }
                 
                    var parkingSession = await _context.ParkingSessions.FirstOrDefaultAsync(
                        ps => ps.ParkingLotId == id &&
                        ps.LicensePlate == normalizedLicensePlate &&
                        ps.Stopped == null
                    );
                
                    if (parkingSession == null)
                    {
                        _logger.LogWarning(
                            "No active session found for ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                            id, normalizedLicensePlate);
                        throw new ArgumentException("Cannot stop a session when there is no session for this licenseplate.");
                    }
                    
                    _logger.LogInformation(
                        "Found session to stop: Id={Id}, ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                        parkingSession.Id, parkingSession.ParkingLotId, parkingSession.LicensePlate);
                    
                    // Use normalized license plate for consistency
                    var reservationWalkOutDto = new WalkOutDto {LicensePlate = normalizedLicensePlate};
                    var stoppedSession = await CreateWalkOutService(reservationWalkOutDto, id);
                    return stoppedSession;
                }
                else
                {
                    _logger.LogInformation(
                    "LicensePlate Is Wrong {licensePlate}",
                    request.LicensePlate);
                    throw new InvalidOperationException($"LicensePlate Is Wrong {request.LicensePlate}");
                }
            }
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException();
                
            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
                throw new KeyNotFoundException();
                
            // Check all active sessions for debugging
            var allActiveSessionsAuth = await _context.ParkingSessions
                .Where(ps => ps.Stopped == null)
                .ToListAsync();
            
            _logger.LogInformation(
                "StopSessionService (authenticated): Found {Count} active sessions total. Looking for ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                allActiveSessionsAuth.Count, id, normalizedLicensePlate);
            
            foreach (var session in allActiveSessionsAuth)
            {
                _logger.LogInformation(
                    "Active session: Id={Id}, ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}, UserId={UserId}",
                    session.Id, session.ParkingLotId, session.LicensePlate, session.UserId);
            }
                
            var activeSession = await _context.ParkingSessions
                .FirstOrDefaultAsync(ps =>
                    ps.ParkingLotId == id &&
                    ps.LicensePlate == normalizedLicensePlate &&
                    ps.Stopped == null);

            if (activeSession == null)
            {
                _logger.LogWarning(
                    "No active session found for ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}, UserId={UserId}",
                    id, normalizedLicensePlate, userId);
                throw new ArgumentException("Cannot stop a session when there is no session for this licenseplate.");
            }
            
            _logger.LogInformation(
                "Found session to stop: Id={Id}, ParkingLotId={ParkingLotId}, LicensePlate={LicensePlate}",
                activeSession.Id, activeSession.ParkingLotId, activeSession.LicensePlate);

            var stoppedTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            if(userId != activeSession.UserId)
            {
                if (!user.IsInRole("Admin") && !user.IsInRole("admin") && !user.IsInRole("ADMIN"))
                    throw new UnauthorizedAccessException();
            }
            // ✅ FIX: Converteer BEIDE naar UTC voor berekening
            var totalMinutes = (stoppedTime - activeSession.Started.ToUniversalTime()).TotalMinutes;

            activeSession.Stopped = stoppedTime;
            activeSession.DurationMinutes = (int)Math.Ceiling(totalMinutes);

            // Bereken cost
            // Reken altijd per volledig uur, afronden naar boven
            decimal durationHours = Math.Ceiling(activeSession.DurationMinutes.Value / 60m);
            decimal fullDays = Math.Floor(durationHours / 24); // 1
            decimal remainingHours = durationHours % 24; 
            decimal tariffPerDay = (decimal)parkingLot.DayTariff;
            decimal tariffPerHour = (decimal)parkingLot.Tariff;
            decimal costCalculated = (fullDays * tariffPerDay) + (remainingHours * tariffPerHour);
            costCalculated = decimal.Round(costCalculated, 2);
            activeSession.Cost = costCalculated;
            activeSession.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Haal username op voor archived_by
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? userId.ToString();

            // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
            if (!_context.Database.IsInMemory())
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Archiveer de sessie naar archived_parking_sessions
                    var archivedSession = new ArchivedParkingSessions
                    {
                        ParkingLotId = activeSession.ParkingLotId,
                        LicensePlate = activeSession.LicensePlate,
                        Started = DateTime.SpecifyKind(activeSession.Started, DateTimeKind.Utc),
                        Stopped = activeSession.Stopped.HasValue 
                            ? DateTime.SpecifyKind(activeSession.Stopped.Value, DateTimeKind.Utc) 
                            : null,
                        UserId = activeSession.UserId,
                        IsWalkUp = activeSession.IsWalkUp,
                        DurationMinutes = activeSession.DurationMinutes,
                        Cost = activeSession.Cost,
                        PaymentStatus = activeSession.PaymentStatus,
                        CreatedAt = DateTime.SpecifyKind(activeSession.CreatedAt, DateTimeKind.Utc),
                        ModifiedAt = activeSession.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(activeSession.ModifiedAt.Value, DateTimeKind.Utc) 
                            : null,
                        OriginalSessionId = activeSession.Id,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        ArchivedBy = username
                    };

                    _context.ArchivedParkingSessions.Add(archivedSession);

                    // Verwijder de originele sessie uit de main tabel
                    _context.ParkingSessions.Remove(activeSession);

                    // Save changes en commit transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Parking sessie {SessionId} gestopt en gearchiveerd - Kenteken: {LicensePlate}, Duur: {Duration} min, Kosten: €{Cost}, Eindtijd: {Stopped}",
                        activeSession.Id, request.LicensePlate, activeSession.DurationMinutes, activeSession.Cost, activeSession.Stopped);
                    
                    // Return activeSession before it was removed (we have all the data we need)
                    return activeSession;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Fout bij archiveren en verwijderen van parking sessie {SessionId}. Error: {ErrorMessage}", 
                        activeSession.Id, ex.Message);
                    throw;
                }
            }
            else
            {
                try
                {
                    // Archiveer de sessie naar archived_parking_sessions
                    var archivedSession = new ArchivedParkingSessions
                    {
                        ParkingLotId = activeSession.ParkingLotId,
                        LicensePlate = activeSession.LicensePlate,
                        Started = DateTime.SpecifyKind(activeSession.Started, DateTimeKind.Utc),
                        Stopped = activeSession.Stopped.HasValue 
                            ? DateTime.SpecifyKind(activeSession.Stopped.Value, DateTimeKind.Utc) 
                            : null,
                        UserId = activeSession.UserId,
                        IsWalkUp = activeSession.IsWalkUp,
                        DurationMinutes = activeSession.DurationMinutes,
                        Cost = activeSession.Cost,
                        PaymentStatus = activeSession.PaymentStatus,
                        CreatedAt = DateTime.SpecifyKind(activeSession.CreatedAt, DateTimeKind.Utc),
                        ModifiedAt = activeSession.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(activeSession.ModifiedAt.Value, DateTimeKind.Utc) 
                            : null,
                        OriginalSessionId = activeSession.Id,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        ArchivedBy = username
                    };
                    _context.ArchivedParkingSessions.Add(archivedSession);

                    // Verwijder de originele sessie uit de main tabel
                    _context.ParkingSessions.Remove(activeSession);

                    // Save changes en commit transaction
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Parking sessie {SessionId} gestopt en gearchiveerd - Kenteken: {LicensePlate}, Duur: {Duration} min, Kosten: €{Cost}, Eindtijd: {Stopped}",
                        activeSession.Id, request.LicensePlate, activeSession.DurationMinutes, activeSession.Cost, activeSession.Stopped);
                    return activeSession;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fout bij archiveren en verwijderen van parking sessie {SessionId}. Error: {ErrorMessage}", 
                        activeSession.Id, ex.Message);
                    throw;
                }
            }
        }
                

        public async Task<ParkingSessions> CreateWalkUpService(WalkUpDto request)
        {
            var parkingSessionCheck = await _context.ParkingSessions
                .FirstOrDefaultAsync(ps =>
                    ps.ParkingLotId == request.ParkingLotId &&
                    ps.LicensePlate == request.LicensePlate.ToUpper() &&
                    ps.Stopped == null);
            if(parkingSessionCheck != null)
            {
                throw new  ArgumentException("Parking session for this vehicle already exists. Drive out!.");
            }
            var parkingLot = await _context.ParkingLots.FindAsync(request.ParkingLotId);
            if (parkingLot == null)
            {
                _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden", request.ParkingLotId);
                throw new KeyNotFoundException("Parkeerplaats niet gevonden");
            }
            // Gebruik UTC tijd - EF Core zal dit correct mappen naar timestamp without time zone
            // Door de DateTime lokaal op te slaan voorkomt we type mismatch issues
            var nowUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var overlappingReservations = await _context.Reservations
                .Where(r => r.ParkingLotId == request.ParkingLotId)
                .Where(r => r.Status != "Cancelled")
                .Where(r => r.EndTime.HasValue && r.StartTime <= nowUtc && r.EndTime.Value > nowUtc)
                .CountAsync();
            int availableSpots = parkingLot.Capacity - overlappingReservations;
            bool isAvailable = availableSpots > 0;
            if(!isAvailable)
            {
                throw new ArgumentException("capacity reached.");
            } 

            // Walk-up: voertuig hoeft NIET in database te staan
            // Parse start date, gebruik UtcNow als fallback
            // BELANGRIJK: Converteer naar Unspecified voor PostgreSQL timestamp without time zone
            DateTime startTime;
            if (!string.IsNullOrEmpty(request.StartDate))
            {
                var parsedTime = ParseDateTimeUnspecified(request.StartDate);
                startTime = parsedTime.HasValue 
                    ? parsedTime.Value.AddHours(1) 
                    : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            }
            else
            {
                startTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            }
            
            var parkingSession = new ParkingSessions
            {
                ParkingLotId = request.ParkingLotId,
                LicensePlate = request.LicensePlate.ToUpper(),
                Started = startTime,
                Stopped = null, 
                UserId = null,
                IsWalkUp = true, // Markeer als walk-up sessie
                DurationMinutes = null,
                Cost = null,
                PaymentStatus = "PENDING",
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                ModifiedAt = null,
                OriginalSessionId = null
            };

            _context.ParkingSessions.Add(parkingSession);
            await _context.SaveChangesAsync();
            
            return parkingSession;
        }

        public async Task<ParkingSessions> CreateWalkOutService(WalkOutDto request, int parkingLotId)
        {
            var parkingSession = await _context.ParkingSessions
                .FirstOrDefaultAsync(ps =>
                    ps.ParkingLotId == parkingLotId &&
                    ps.LicensePlate == request.LicensePlate.ToUpper() &&
                    ps.Stopped == null);
            if(parkingSession == null)
            {
                throw new ArgumentException("No parkingSession.");
            }
            var parkingLot = await _context.ParkingLots.FindAsync(parkingLotId);
            if (parkingLot == null)
            {
                _logger.LogWarning("Parkeerplaats {ParkingLotId} niet gevonden", parkingLotId);
                throw new KeyNotFoundException("Parkeerplaats niet gevonden");
            }

            // Voor walk-up sessies hoeft het voertuig NIET in de database te staan
            // Alleen voor normale sessies (met userId) moet het voertuig bestaan
            if (!parkingSession.IsWalkUp)
            {
                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == request.LicensePlate.ToUpper());

                if (vehicle == null)
                {
                    _logger.LogWarning("Voertuig met kenteken {LicensePlate} niet gevonden", request.LicensePlate);
                    throw new ArgumentException("Kenteken niet gevonden of niet van jou");
                }
            }
            else
            {
                _logger.LogInformation(
                    "Walk-up sessie voor kenteken {LicensePlate} - voertuig hoeft niet in database te staan",
                    request.LicensePlate);
            }
            var startTime = ParseDateTimeUnspecified(parkingSession.Started.ToString("yyyy-MM-dd HH:mm:ss"));
            var endTime = ParseDateTimeUnspecified(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            if (startTime == null || endTime == null)
            {
                _logger.LogWarning("Ongeldige datum formaat bij update: StartDate={StartDate}, EndDate={EndDate}", 
                    startTime, endTime);
                throw new ArgumentException("Ongeldig datum formaat. Gebruik YYYY-MM-DD HH:MM:SS");
            }
            if (startTime.Value >= endTime.Value.AddHours(1))
            {
                _logger.LogWarning("Eindtijd {EndTime} is niet na starttijd {StartTime}", 
                    endTime.Value, startTime.Value);
                throw new KeyNotFoundException("Eindtijd moet na starttijd zijn");
            }

            // BEREKEN KOSTEN
            var durationHours = (decimal)(endTime.Value.AddHours(1) - startTime.Value).TotalHours;
            var calculatedCost = decimal.Round(durationHours * Convert.ToDecimal(parkingLot.Tariff), 2, MidpointRounding.AwayFromZero);

            _logger.LogDebug("Kosten berekend: {Hours} uur × €{Tariff} = €{Cost}", 
                durationHours, parkingLot.Tariff, calculatedCost);
            
            parkingSession.Stopped = DateTime.SpecifyKind(endTime.Value.AddHours(1), DateTimeKind.Unspecified);
            parkingSession.DurationMinutes = (int)(decimal)(endTime.Value.AddHours(1) - startTime.Value).TotalMinutes;
            parkingSession.Cost = calculatedCost;
            parkingSession.PaymentStatus = "Confirmed";
            parkingSession.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Archiveer walk-up sessie naar archived_parking_sessions
            // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Converteer DateTime waarden naar UTC voor PostgreSQL
                // Als DateTime al Unspecified is (komt uit PostgreSQL), behandel het als UTC
                var startedUtc = parkingSession.Started.Kind == DateTimeKind.Utc 
                    ? parkingSession.Started 
                    : (parkingSession.Started.Kind == DateTimeKind.Local 
                        ? parkingSession.Started.ToUniversalTime() 
                        : new DateTime(parkingSession.Started.Ticks, DateTimeKind.Utc));
                
                var stoppedUtc = parkingSession.Stopped.HasValue
                    ? (parkingSession.Stopped.Value.Kind == DateTimeKind.Utc
                        ? parkingSession.Stopped.Value
                        : (parkingSession.Stopped.Value.Kind == DateTimeKind.Local
                            ? parkingSession.Stopped.Value.ToUniversalTime()
                            : new DateTime(parkingSession.Stopped.Value.Ticks, DateTimeKind.Utc)))
                    : (DateTime?)null;

                var createdAtUtc = parkingSession.CreatedAt.Kind == DateTimeKind.Utc
                    ? parkingSession.CreatedAt
                    : (parkingSession.CreatedAt.Kind == DateTimeKind.Local
                        ? parkingSession.CreatedAt.ToUniversalTime()
                        : new DateTime(parkingSession.CreatedAt.Ticks, DateTimeKind.Utc));

                var modifiedAtUtc = parkingSession.ModifiedAt.HasValue
                    ? (parkingSession.ModifiedAt.Value.Kind == DateTimeKind.Utc
                        ? parkingSession.ModifiedAt.Value
                        : (parkingSession.ModifiedAt.Value.Kind == DateTimeKind.Local
                            ? parkingSession.ModifiedAt.Value.ToUniversalTime()
                            : new DateTime(parkingSession.ModifiedAt.Value.Ticks, DateTimeKind.Utc)))
                    : (DateTime?)null;

                // Archiveer de sessie naar archived_parking_sessions
                var archivedSession = new ArchivedParkingSessions
                {
                    ParkingLotId = parkingSession.ParkingLotId,
                    LicensePlate = parkingSession.LicensePlate,
                    Started = startedUtc,
                    Stopped = stoppedUtc,
                    UserId = parkingSession.UserId,
                    IsWalkUp = parkingSession.IsWalkUp,
                    DurationMinutes = parkingSession.DurationMinutes,
                    Cost = parkingSession.Cost,
                    PaymentStatus = parkingSession.PaymentStatus,
                    CreatedAt = createdAtUtc,
                    ModifiedAt = modifiedAtUtc,
                    OriginalSessionId = parkingSession.Id,
                    ArchivedAt = DateTime.UtcNow,
                    ArchivedBy = "WALKUP" // Walk-up sessies worden gearchiveerd door systeem
                };

                _context.ArchivedParkingSessions.Add(archivedSession);

                // Verwijder de originele sessie uit de main tabel
                _context.ParkingSessions.Remove(parkingSession);

                // Save changes en commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Walk-up parking sessie {SessionId} gestopt en gearchiveerd - Kenteken: {LicensePlate}, Duur: {Duration} min, Kosten: €{Cost}, Eindtijd: {Stopped}",
                    parkingSession.Id, request.LicensePlate, parkingSession.DurationMinutes, parkingSession.Cost, parkingSession.Stopped);
                
                // Return parkingSession before it was removed (we have all the data we need)
                return parkingSession;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Fout bij archiveren en verwijderen van walk-up parking sessie {SessionId}. Error: {ErrorMessage}, StackTrace: {StackTrace}", 
                    parkingSession.Id, ex.Message, ex.StackTrace);
                _logger.LogError("ParkingSession details: Id={Id}, LicensePlate={LicensePlate}, Started={Started}, Stopped={Stopped}, CreatedAt={CreatedAt}, ModifiedAt={ModifiedAt}",
                    parkingSession.Id, parkingSession.LicensePlate, parkingSession.Started, parkingSession.Stopped, parkingSession.CreatedAt, parkingSession.ModifiedAt);
                throw;
            }
        }

        // Hulpfunctie: Parse string naar UTC DateTime
        private DateTime? ParseDateTimeUnspecified(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            }
            return null;
        }


        public async Task<ParkingLots?> CreateParkingLotService(CreateParkingLotRequestDto request, ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");
            if(!user.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Access Denied");
            }

            // Validatie: Capacity moet groter zijn dan Reserved
            if (request.Capacity < request.Reserved)
                throw new ArgumentException("Capacity cannot be less than Reserved places");

            // Validatie: Verplichte velden mogen niet leeg zijn
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Name is required");
            
            if (string.IsNullOrWhiteSpace(request.Location))
                throw new ArgumentException("Location is required");
            
            if (string.IsNullOrWhiteSpace(request.Postcode))
                throw new ArgumentException("Postcode is required");
            
            if (request.HouseNumber <= 0)
                throw new ArgumentException("HouseNumber must be greater than 0");
                    
            // Validatie: Capacity en Tariff moeten positief zijn
            if (request.Capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0");
            
            if (request.Tariff < 0)
                throw new ArgumentException("Tariff cannot be negative");
            
            // Validatie: Coördinaten moeten geldig zijn
            if (request.Lat < -90 || request.Lat > 90)
                throw new ArgumentException("Latitude must be between -90 and 90");
            
            if (request.Lng < -180 || request.Lng > 180)
                throw new ArgumentException("Longitude must be between -180 and 180");

            PdokDocAddressResponseDto address = await _addressValidationService.GetAddressAsync(request.Postcode, request.HouseNumber);
            Console.WriteLine($"dit is het adress1: {address}");
            if (address == null)
                throw new ArgumentException("Adres bestaat niet volgens het Kadaster (BAG)");
            var addressString = $"{address.straatnaam} {address.huisnummer} {address.postcode} {address.woonplaatsnaam}";
            Console.WriteLine($"dit is het adress2: {addressString}");
            if(addressString.Length <= 3)
                throw new ArgumentException("Adres is ongeldig");
            if(!addressString.Contains(request.Postcode) || !addressString.Contains(request.HouseNumber.ToString()))
                throw new ArgumentException("Adres komt niet overeen met opgegeven postcode en huisnummer");

            var coordinates = $"{{\"lat\": {request.Lat}, \"lng\": {request.Lng}}}";
            
            var parkingLot = new ParkingLots
            {
                Name = request.Name,
                Location = request.Location,
                Address = addressString,
                Capacity = request.Capacity,
                Reserved = request.Reserved,
                Tariff = request.Tariff,
                DayTariff = request.DayTariff,
                Coordinates = coordinates, 
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.ParkingLots.Add(parkingLot);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Parking lot {ParkingLotId} aangemaakt door admin {UserId}",
                parkingLot.Id, userId);

            return parkingLot;
        }

        
        public async Task<ParkingLots> UpdateParkingLotService(int id, CreateParkingLotRequestDto request, ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");
            if(!user.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Access Denied");
            }

            if (string.IsNullOrWhiteSpace(request.Postcode))
                throw new ArgumentException("Postcode is required");

            if (request.HouseNumber <= 0)
                throw new ArgumentException("HouseNumber must be greater than 0");

            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
                throw new KeyNotFoundException();
            if (request.Capacity < request.Reserved)
                throw new ArgumentException();
            
            var address = $"{request.Postcode} {request.HouseNumber}";
            var coordinates = $"{{\"lat\": {request.Lat}, \"lng\": {request.Lng}}}";

            parkingLot.Name = request.Name;
            parkingLot.Location = request.Location;
            parkingLot.Address = address;
            parkingLot.Capacity = request.Capacity;
            parkingLot.Reserved = request.Reserved;
            parkingLot.Tariff = request.Tariff;
            parkingLot.DayTariff = request.DayTariff;
            parkingLot.Coordinates = coordinates; 
            parkingLot.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Parking lot {ParkingLotId} geüpdatet door admin {UserId}",
                parkingLot.Id, userId);

            return parkingLot;
        }
        
       
        public async Task<ParkingLots?> DeleteParkingLotService(int id, ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");
            if(!user.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Access Denied");
            }
            // Zoek parking lot
            var parkingLot = await _context.ParkingLots.FindAsync(id);
            if (parkingLot == null)
                throw new KeyNotFoundException();

            // Check of er actieve sessions zijn
            var activeSessions = await _context.ParkingSessions
                .AnyAsync(ps => ps.ParkingLotId == id && ps.Stopped == null);

            if (activeSessions)
                throw new ArgumentException();

            // Haal username op voor archived_by
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? userId.ToString();

            // Gebruik database transaction om ervoor te zorgen dat alles of niets wordt gearchiveerd
            if (!_context.Database.IsInMemory())
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Archiveer het parking lot naar archived_parking_lots
                    var archivedParkingLot = new ArchivedParkingLots
                    {
                        Name = parkingLot.Name,
                        Location = parkingLot.Location,
                        Address = parkingLot.Address,
                        Capacity = parkingLot.Capacity,
                        Reserved = parkingLot.Reserved,
                        Tariff = (double)parkingLot.Tariff,
                        DayTariff = parkingLot.DayTariff.ToString(),
                        CreatedAt = parkingLot.CreatedAt.HasValue 
                            ? DateTime.SpecifyKind(parkingLot.CreatedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        ModifiedAt = parkingLot.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(parkingLot.ModifiedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        Coordinates = parkingLot.Coordinates,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = username
                    };

                    _context.ArchivedParkingLots.Add(archivedParkingLot);

                    // Verwijder het parking lot uit de main tabel
                    _context.ParkingLots.Remove(parkingLot);

                    // Save changes en commit transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Parking lot {ParkingLotId} ({Name}) succesvol gearchiveerd en verwijderd door {ArchivedBy}",
                        id, parkingLot.Name, username);

                    return parkingLot;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Fout bij archiveren en verwijderen van parking lot {ParkingLotId}", id);
                    throw;
                }
            }
            else
            {
                try
                {
                    var archivedParkingLot = new ArchivedParkingLots
                    {
                        Name = parkingLot.Name,
                        Location = parkingLot.Location,
                        Address = parkingLot.Address,
                        Capacity = parkingLot.Capacity,
                        Reserved = parkingLot.Reserved,
                        Tariff = (double)parkingLot.Tariff,
                        DayTariff = parkingLot.DayTariff.ToString(),
                        CreatedAt = parkingLot.CreatedAt.HasValue 
                            ? DateTime.SpecifyKind(parkingLot.CreatedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        ModifiedAt = parkingLot.ModifiedAt.HasValue 
                            ? DateTime.SpecifyKind(parkingLot.ModifiedAt.Value, DateTimeKind.Unspecified)
                            : null,
                        Coordinates = parkingLot.Coordinates,
                        ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                        ArchivedBy = username
                    };

                    _context.ArchivedParkingLots.Add(archivedParkingLot);
                    // In-memory database: geen transaction nodig
                    _context.ParkingLots.Remove(parkingLot);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Parking lot {ParkingLotId} ({Name}) succesvol verwijderd door {ArchivedBy} (in-memory DB)",
                        id, parkingLot.Name, username);

                    return parkingLot;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fout bij archiveren en verwijderen van parking lot {ParkingLotId}", id);
                    throw;
                }
            }
        }

        private bool IsValidDutchLicensePlate(string licensePlate)
        {
            if (string.IsNullOrWhiteSpace(licensePlate))
                return false;

            // Verwijder streepjes en spaties, maak hoofdletters
            var cleaned = licensePlate.Replace("-", "").Replace(" ", "").ToUpper();
        
            // Check lengte (6-8 karakters voor Nederlandse kentekens)
            if (cleaned.Length < 6 || cleaned.Length > 8)
                return false;
        
            // Nederlandse kenteken formaten
            var patterns = new[]
            {
                // 6 karakters
                @"^[A-Z]{2}\d{2}\d{2}$",     // XX-99-99
                @"^\d{2}[A-Z]{2}\d{2}$",     // 99-XX-99
                @"^\d{2}\d{2}[A-Z]{2}$",     // 99-99-XX
                @"^[A-Z]{2}\d{2}[A-Z]{2}$",  // XX-99-XX
                @"^[A-Z]{2}[A-Z]{2}\d{2}$",  // XX-XX-99
                @"^\d{2}[A-Z]{2}[A-Z]{2}$",  // 99-XX-XX
            
                // 7 karakters
                @"^[A-Z]{2}\d{3}[A-Z]$",     // XX-999-X (bijv. AB-123-C)
                @"^[A-Z]\d{3}[A-Z]{2}$",     // X-999-XX
                @"^\d[A-Z]{2}\d{3}$",        // 9-XX-999
                @"^\d{3}[A-Z]{2}\d$",        // 999-XX-9
                @"^[A-Z]{3}\d{2}[A-Z]$",     // XXX-99-X
                @"^[A-Z]\d{2}[A-Z]{3}$",     // X-99-XXX
                @"^\d[A-Z]{3}\d{2}$",        // 9-XXX-99
                @"^\d{3}[A-Z]{3}$",          // 999-XXX
            
                // 8 karakters (oudere formaten)
                @"^[A-Z]{2}\d{4}$",          // XX-9999
                @"^\d{4}[A-Z]{2}$",          // 9999-XX
                @"^[A-Z]{3}\d{3}$",          // XXX-999
                @"^\d{3}[A-Z]{3}$"           // 999-XXX
            };
        
            return patterns.Any(p => Regex.IsMatch(cleaned, p));
        }
    }
}
