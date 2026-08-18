using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobyParkApi.Service;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;

namespace MobyParkApi.Controllers;

[SwaggerOrder(4)]
[ApiController]
[Route("api/parking-lots")]
[Authorize]
public class ParkingLotsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ParkingLotsController> _logger;
    private readonly ParkingLotService _parkingLotService;
    private readonly ILogger<ReservationController> _reservationLogger;
    private readonly IAddressValidationService _addressValidation;

    public ParkingLotsController(ApplicationDbContext context, ILogger<ParkingLotsController> logger, ILogger<ReservationController> reservationLogger, ReservationService reservationService, IAddressValidationService addressValidationService)
    {
        _context = context;
        _logger = logger;
        _addressValidation = addressValidationService;
        _reservationLogger = reservationLogger;
        _parkingLotService = new ParkingLotService(_context, _logger, _reservationLogger, reservationService, _addressValidation);
    }

    /// <summary>
    /// GET /api/parking-lots - Haal alle parking lots op (ADMIN ziet alles users krijgen beschikbare te zien)
    /// </summary>
    [HttpGet()]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllParkingLots(
        [FromQuery] string sortBy = "name", // name, location, capacity, available
        [FromQuery] string order = "asc", // asc, desc
        [FromQuery] int page = 1, // pagina nummer
        [FromQuery] int pageSize = 10) // items per pagina
    {
        try 
        {
            // gebruiker ophalen via claim 
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // gebruiker zoeken, als hij null is foutmelding weergeven
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Gebruiker niet gevonden in token");

            // rol ophalen, als er geen rol gevonden standaard user gebruiken
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            if (page < 1)
                page = 1;
            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            // admin ziet alles, inclusief inactieve parkeerplaatsen
            if (userRole == "Admin") {
                _logger.LogInformation("Admin haalt alle parkeerplaatsen op");

                // data tellen om te bepalen hoeveel pages er moeten komen
                var totalCount = await _context.ParkingLots.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var allParkingLots = await _context.ParkingLots
                                    .OrderByDescending(p => p.CreatedAt)
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync();
                
                // sorteren
                var sortedLots = SortParkingLots(allParkingLots, sortBy, order);
                
                // response met pagination info
                return Ok(new 
                {
                    data = sortedLots,
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
            else
             {
                _logger.LogInformation("Gebruiker haalt beschikbare parkeerplaatsen op (pagina {Page}, {PageSize} items)", page, pageSize);
                
                //lle beschikbare ophalen (service bepaalt welke beschikbaar zijn)
                var parkingLots = await _parkingLotService.GetAllParkingLotsService(sortBy, order);
                
                // pagination toepassen
                var totalCount = parkingLots.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                var paginatedLots = parkingLots
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                
                // response met pagination info
                return Ok(new
                {
                    data = paginatedLots,
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen parkinglots");
            return StatusCode(500, "Er is een fout opgetreden");
        }
        
    }

    /// <summary>
    /// GET /api/parking-lots/{id} - Haal een specifieke parking lot op
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetParkingLotById(int id)
    {
        try
        {
            var parkingLot = await _parkingLotService.GetParkingLotByIdService(id);
            return Ok(new
            {
                id = parkingLot.Id,
                name = parkingLot.Name,
                location = parkingLot.Location,
                postcode = parkingLot.Address,
                capacity = parkingLot.Capacity,
                reserved = parkingLot.Reserved,
                tariff = parkingLot.Tariff,
                dayTariff = parkingLot.DayTariff,
                createdAt = parkingLot.CreatedAt,
                coordinates = parkingLot.Coordinates,
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        }
        catch (ArgumentException)
        {
            return BadRequest("Parking lot is vol");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen parking lot {ParkingLotId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// GET /api/parking-lots/{id}/sessions - Haal alle sessions van een parking lot op
    /// </summary>
    [HttpGet("{id}/sessions")]
    [Authorize]
    public async Task<IActionResult> GetParkingLotSessions(
        int id,
        [FromQuery] bool activeOnly = false)
    {
        try
        {
            var parkingSessions = await _parkingLotService.GetParkingLotSessionsService(id, User, activeOnly);

            return Ok(parkingSessions);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("Gebruiker niet gevonden in token");
        }
        catch(KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        } 
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen sessions voor parking lot {ParkingLotId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// POST /api/parking-lots/{id}/sessions/start - Start een parking sessie
    /// </summary>
    [HttpPost("{id}/sessions/start")]
    [AllowAnonymous]
    public async Task<IActionResult> StartSession(int id, StartSessionRequestDto request)
    {
        try
        {
            await _parkingLotService.StartSessionService(id, request, User);
            return Ok($"Session started for: {request.LicensePlate}");
        }
        catch(KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        }
        catch (ArgumentException)
        {
            return BadRequest("Cannot start a session when another session for this licenseplate is already started.");
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("Gebruiker niet gevonden in token");
        }
        catch(InvalidOperationException IOE)
        {
            return BadRequest(IOE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij starten parking sessie voor parking lot {ParkingLotId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// POST /api/parking-lots/{id}/sessions/stop - Stop een parking sessie
    /// </summary>
    [HttpPost("{id}/sessions/stop")]
    [AllowAnonymous]
    public async Task<IActionResult> StopSession(int id, StopSessionRequestDto request)
    {
        try
        {
            var activeSession = await _parkingLotService.StopSessionService(id, request, User);
            if (activeSession == null)
                return NotFound("Session not found");
            
            return Ok(new
            {
                message = $"Session stopped and archived for: {request.LicensePlate}",
                sessionId = activeSession.Id,
                durationMinutes = activeSession.DurationMinutes,
                cost = activeSession.Cost,
                stoppedTime = activeSession.Stopped
            });
        }
        catch(ArgumentException)
        {
            return BadRequest("Cannot stop a session when there is no session for this licenseplate.");
        }
        catch(KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        }
        catch(UnauthorizedAccessException)
        {
            return Unauthorized("Gebruiker niet gevonden in token");
        }
        catch(InvalidOperationException IOE)
        {
            return BadRequest(IOE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij stoppen parking sessie: {ErrorMessage}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = "Er is een fout opgetreden", message = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>
    /// POST /api/parking-lots - Maak een nieuwe parking lot aan (ADMIN only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateParkingLot(CreateParkingLotRequestDto request)
    {
        try
        {
            var createdParkingLot = await _parkingLotService.CreateParkingLotService(request, User);
            return CreatedAtAction(
                nameof(GetParkingLotById),
                new { id = createdParkingLot.Id },
                new
                {
                    message = $"Parking lot saved under ID: {createdParkingLot.Id}",
                    parkingLot = createdParkingLot
                });
        }
        catch (UnauthorizedAccessException UAE)
        {
            return Unauthorized($"{UAE.Message}");
        }
        catch (ArgumentException AE)
        {
            return BadRequest($"{AE.Message}");
        }   
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij aanmaken parking lot");
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// PUT /api/parking-lots/{id} - Update een parking lot (ADMIN only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateParkingLot(int id, CreateParkingLotRequestDto request)
    {
        try
        {
            var updatedParkingLot = await _parkingLotService.UpdateParkingLotService(id, request, User);

            return Ok(new
            {
                message = $"Parking lot {id} successfully updated",
                id = updatedParkingLot.Id,
                name = updatedParkingLot.Name,
                location = updatedParkingLot.Location,
                postcode = updatedParkingLot.Address,
                capacity = updatedParkingLot.Capacity,
                reserved = updatedParkingLot.Reserved,
                tariff = updatedParkingLot.Tariff,
                dayTariff = updatedParkingLot.DayTariff,
                createdAt = updatedParkingLot.CreatedAt,
                coordinates = updatedParkingLot.Coordinates,
            });
        }
        catch (UnauthorizedAccessException UAE)
        {
            return Unauthorized($"{UAE.Message}");
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }   
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij updaten parking lot {ParkingLotId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }
    
    /// <summary>
    /// DELETE /api/parking-lots/{id} - Verwijder een parking lot (ADMIN only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteParkingLot(int id)
    {
        try
        {
            await _parkingLotService.DeleteParkingLotService(id, User);
            return Ok(new { message = $"Parking lot {id} successfully deleted" });
        }
        catch(UnauthorizedAccessException UAE)
        {
            return Unauthorized($"{UAE.Message}");
        }
        catch(KeyNotFoundException)
        {
            return NotFound($"Parking lot met ID {id} niet gevonden");
        }   
        catch(ArgumentException)
        {
            return BadRequest("Cannot delete parking lot with active sessions. Stop all sessions first.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij verwijderen parking lot {ParkingLotId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// Helper methode voor sorteren perkeerplekken
    /// </summary>
    private List<ParkingLots> SortParkingLots(List<ParkingLots> parkingLots, string sortBy, string order)
    {
        // Fix: Gebruik OrderByDescending voor desc in plaats van Reverse()
        var isDescending = order.ToLower() == "desc";
        
        IEnumerable<ParkingLots> sorted = sortBy.ToLower() switch
        {
            "id" => isDescending 
                ? parkingLots.OrderByDescending(p => p.Id) 
                : parkingLots.OrderBy(p => p.Id),
            "location" => isDescending 
                ? parkingLots.OrderByDescending(p => p.Location) 
                : parkingLots.OrderBy(p => p.Location),
            "capacity" => isDescending 
                ? parkingLots.OrderByDescending(p => p.Capacity) 
                : parkingLots.OrderBy(p => p.Capacity),
            "available" => isDescending 
                ? parkingLots.OrderByDescending(p => p.Capacity - p.Reserved) 
                : parkingLots.OrderBy(p => p.Capacity - p.Reserved),
            _ => isDescending 
                ? parkingLots.OrderByDescending(p => p.Name) 
                : parkingLots.OrderBy(p => p.Name)
        };

        return sorted.ToList();
    }
}