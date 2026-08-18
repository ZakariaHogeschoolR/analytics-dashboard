using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using MobyParkApi.Data;
using Microsoft.EntityFrameworkCore;

namespace MobyParkApi.Controllers
{
    [SwaggerOrder(5)]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReservationController> _logger;
        private readonly IReservationAutoCompleteService _autoCompleteService;

        public ReservationController(
            IReservationService reservationService, 
            ApplicationDbContext context,
            ILogger<ReservationController> logger,
            IReservationAutoCompleteService autoCompleteService)
        {
            _reservationService = reservationService;
            _context = context;
            _logger = logger;
            _autoCompleteService = autoCompleteService;
        }
        
        [HttpGet]
        public async Task<IActionResult> GetAllReservations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // gebruiker ophalen
                var currentUser = await GetCurrentUserAsync();

                // kijken of gebruiker gevonden is, zo niet error weergeven
                if (currentUser == null)
                {
                    _logger.LogWarning("GetAllReservations: Gebruiker niet gevonden in context");
                    return Unauthorized(new { error = "Gebruiker niet gevonden" });
                }

                // als userRole null is user als rol gebruiken
                var userRole = currentUser.Role ?? "User";

                // pagination parameters
                if (page < 1)
                    page = 1;
                if (pageSize < 1 || pageSize > 100)
                    pageSize = 10;

                // kijken of rol admin is, if true alle gegevens ophalen
                if (userRole == "Admin")
                {
                    _logger.LogInformation("Admin haalt alle reserveringen op (pagina {Page}, {PageSize} items)", page, pageSize);

                    var now = DateTime.UtcNow;
                    // Filter: alleen actieve reserveringen (EndTime >= NOW() OR EndTime IS NULL)
                    var totalCount = await _context.Reservations
                        .Where(r => r.EndTime == null || r.EndTime >= now)
                        .CountAsync();
                    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize); 

                    var allReservations = await _context.Reservations
                        .Where(r => r.EndTime == null || r.EndTime >= now)
                        .OrderByDescending(r => r.CreatedAt)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();
                    
                    // Auto-complete check voor alle reservations
                    foreach (var reservation in allReservations)
                    {
                        await _autoCompleteService.CheckAndCompleteReservationAsync(reservation);
                    }
                    
                    return Ok(new
                    {
                        data = allReservations,
                        pagination = new
                        {
                            currentPage = page,
                            pageSize = pageSize,
                            totalCount = totalCount,
                            totalPages = totalPages,
                            hasNextPage = page < totalPages,
                            hasPreviousPage = page > 1
                        }
                    });
                }
                // Als de rol geen admin is gegevens ophalen van de ingelogde gebruiker
                else
                {
                    _logger.LogInformation("Ophalen van alle reserveringen voor user {UserId}", currentUser.Id);
                    
                    // Haal eerst de reservations uit de database voor auto-complete
                    var userReservations = await _context.Reservations
                        .Where(r => r.UserId == currentUser.Id)
                        .ToListAsync();

                    // Auto-complete check voor user reservations
                    foreach (var reservation in userReservations)
                    {
                        await _autoCompleteService.CheckAndCompleteReservationAsync(reservation);
                    }

                    // Nu via service ophalen (DTOs) - deze zullen de geüpdatete status hebben
                    var reservations = await _reservationService.GetAllUserReservationsAsync(currentUser.Id);

                    _logger.LogInformation("Succesvol {Count} reserveringen opgehaald voor user {UserId}", 
                        reservations.Count, currentUser.Id);
                    return Ok(reservations);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen van reserveringen");
                return StatusCode(500, new { error = "Er is een fout opgetreden bij het ophalen van reserveringen" });
            }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReservationById(int id)
        {
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    _logger.LogWarning("GetReservationById: Gebruiker niet gevonden in context");
                    return Unauthorized(new { error = "Gebruiker niet gevonden" });
                }

                _logger.LogInformation("Ophalen van reservering {ReservationId} voor user {UserId}", 
                    id, currentUser.Id);

                // ⭐ EXTRA DEBUG LOGGING ⭐
                _logger.LogWarning("DEBUG: About to fetch reservation from DB");
                
                // Haal eerst de reservation uit database voor auto-complete check
                var reservationEntity = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == id);

                _logger.LogWarning("DEBUG: Reservation fetched, is null? {IsNull}", reservationEntity == null);
                
                if (reservationEntity != null)
                {
                    _logger.LogWarning("DEBUG: Reservation status: {Status}, UserId: {UserId}, EndTime: {EndTime}", 
                        reservationEntity.Status, reservationEntity.UserId, reservationEntity.EndTime);
                    
                    // Check of user toegang heeft (of admin is)
                    if (reservationEntity.UserId == currentUser.Id || currentUser.Role == "Admin")
                    {
                        _logger.LogWarning("DEBUG: User has access - calling auto-complete service");
                        // Auto-complete check
                        var wasCompleted = await _autoCompleteService.CheckAndCompleteReservationAsync(reservationEntity);
                        _logger.LogWarning("DEBUG: Auto-complete service returned: {WasCompleted}", wasCompleted);
                    }
                    else
                    {
                        _logger.LogWarning("DEBUG: User has NO access - UserId {ResUserId} vs {CurrentUserId}, Role: {Role}", 
                            reservationEntity.UserId, currentUser.Id, currentUser.Role);
                    }
                }

                // Nu via service ophalen (DTO) - deze zal de geüpdatete status hebben
                var reservation = await _reservationService.GetReservationByIdAsync(
                    id, 
                    currentUser.Id, 
                    currentUser.Role
                );

                if (reservation == null)
                {
                    _logger.LogWarning("Reservering {ReservationId} niet gevonden", id);
                    return NotFound(new { error = "Reservering niet gevonden" });
                }

                _logger.LogInformation("Reservering {ReservationId} succesvol opgehaald", id);
                return Ok(reservation);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Ongeautoriseerde toegang tot reservering {ReservationId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: Exception in GetReservationById: {Message}", ex.Message);
                throw;
            }
        }

        [Authorize(Roles = "Admin,User")]
        [HttpPost]
        public async Task<IActionResult> CreateReservation([FromBody] ReservationDto request)
        {
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    _logger.LogWarning("CreateReservation: Gebruiker niet gevonden in context");
                    return Unauthorized(new { error = "Gebruiker niet gevonden" });
                }

                _logger.LogInformation("Aanmaken van reservering voor user {UserId}, parkeerplaats {ParkingLotId}", 
                    currentUser.Id, request.ParkingLotId);

                var reservation = await _reservationService.CreateReservationAsync(
                    request, 
                    currentUser.Id
                );

                _logger.LogInformation("Reservering {ReservationId} aangemaakt voor user {UserId}, parkeerplaats {ParkingLotId}", 
                    reservation.Id, currentUser.Id, request.ParkingLotId);

                return CreatedAtAction(
                    nameof(GetReservationById), 
                    new { id = reservation.Id }, 
                    reservation
                );
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource niet gevonden bij aanmaken reservering: {Message}", ex.Message);
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validatie fout bij aanmaken reservering: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij aanmaken van reservering voor user {UserId}", 
                    (await GetCurrentUserAsync())?.Id);
                return StatusCode(500, new { error = "Er is een fout opgetreden bij het aanmaken van de reservering" });
            }
        }


        [Authorize(Roles = "Admin,User")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReservation(int id, [FromBody] ReservationDto request)
        {
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    _logger.LogWarning("UpdateReservation: Gebruiker niet gevonden in context");
                    return Unauthorized(new { error = "Gebruiker niet gevonden" });
                }

                _logger.LogInformation("Updaten van reservering {ReservationId} voor user {UserId}", 
                    id, currentUser.Id);

                var reservation = await _reservationService.UpdateReservationAsync(
                    id, 
                    request, 
                    currentUser.Id, 
                    currentUser.Role
                );

                _logger.LogInformation("Reservering {ReservationId} succesvol geüpdatet voor user {UserId}", 
                    id, currentUser.Id);

                return Ok(reservation);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Reservering {ReservationId} niet gevonden voor update", id);
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Ongeautoriseerde update poging voor reservering {ReservationId}", id);
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validatie fout bij updaten reservering {ReservationId}: {Message}", 
                    id, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten van reservering {ReservationId}", id);
                return StatusCode(500, new { error = "Er is een fout opgetreden bij het updaten van de reservering" });
            }
        }
    

        [Authorize(Roles = "Admin,User")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    _logger.LogWarning("DeleteReservation: Gebruiker niet gevonden in context");
                    return Unauthorized(new { error = "Gebruiker niet gevonden" });
                }

                _logger.LogInformation("Annuleren van reservering {ReservationId} voor user {UserId} ({Username})", 
                    id, currentUser.Id, currentUser.Username);

                await _reservationService.DeleteReservationAsync(
                    id, 
                    currentUser.Id, 
                    currentUser.Role ?? "User",
                    currentUser.Username
                );

                _logger.LogInformation("Reservering {ReservationId} succesvol geannuleerd door user {UserId}", 
                    id, currentUser.Id);

                return Ok(new { message = "Reservering succesvol geannuleerd en gearchiveerd", reservationId = id });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Reservering {ReservationId} niet gevonden voor annulering", id);
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Ongeautoriseerde annuleer poging voor reservering {ReservationId}", id);
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validatie fout bij annuleren reservering {ReservationId}: {Message}",
                    id, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij annuleren van reservering {ReservationId}", id);
                return StatusCode(500, new { error = "Er is een fout opgetreden bij het annuleren van de reservering" });
            }
        }

        /// <summary>
        /// POST /api/reservation/complete-expired
        /// Batch endpoint om alle verlopen reservations te completen en payments aan te maken
        /// </summary>
        [HttpPost("complete-expired")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CompleteExpiredReservations()
        {
            try
            {
                _logger.LogInformation("Starting batch completion of expired reservations");

                var completedCount = await _autoCompleteService.CheckAndCompleteAllExpiredReservationsAsync();

                _logger.LogInformation("Batch completion finished: {Count} reservations completed", completedCount);

                return Ok(new
                {
                    message = $"Auto-completed {completedCount} expired reservations",
                    count = completedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch completion of expired reservations");
                return StatusCode(500, new { error = "Er is een fout opgetreden bij het completen van verlopen reserveringen" });
            }
        }

        // Helper method om current user op te halen
        private async Task<MobyParkApi.Models.Users?> GetCurrentUserAsync()
        {
            var principal = HttpContext?.User;
            var username = principal?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }
    }
}