using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;

namespace MobyParkApi.Controllers;

[SwaggerOrder(3)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VehiclesController> _logger;
    private readonly VehiclesService _vehicleService;

    public VehiclesController(ApplicationDbContext context, ILogger<VehiclesController> logger, IArchiveService archiveService)
    {
        _context = context;
        _logger = logger;
        _vehicleService = new VehiclesService(_context, _logger, archiveService);
    }

    #region GET Endpoints

    /// <summary>
    /// GET /api/vehicles - Haal alle voertuigen op van de ingelogde gebruiker
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyVehicles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            List<Vehicles> vehicles = await _vehicleService.GetMyVehiclesService(User);
            return Ok(vehicles);            
        }
        catch(UnauthorizedAccessException UAE)
        {
            return Unauthorized(UAE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen voertuigen");
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    /// <summary>
    /// GET /api/vehicles/all - Haal alle voertuigen op (ADMIN only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAllVehicles()
    {
        try
        {
            IEnumerable<object> result = await _vehicleService.GetAllVehiclesService();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen alle voertuigen");
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }
    

    /// <summary>
    /// GET /api/vehicles/{id} - Haal een specifiek voertuig op
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetVehicle(int id)
    {
        try
        {
            Vehicles vehicle = await _vehicleService.GetVehicleService(id, User);

            return Ok(vehicle);
        }
        catch(UnauthorizedAccessException UAE)
        {
            if(UAE.Message == "Access denied")
            {
                return Forbid();
            }
            return Unauthorized(UAE.Message);
        }
        catch(KeyNotFoundException KNFE)
        {

            return NotFound(KNFE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen voertuig {VehicleId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    #endregion

    #region POST Endpoints

    /// <summary>
    /// POST /api/vehicles - Maak een nieuw voertuig aan
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateVehicle(CreateVehicleRequestDto request)
    {
        try
        {
            Vehicles vehicle = await _vehicleService.CreateVehicleService(request, User);
            return Ok(vehicle);
        }
        catch(UnauthorizedAccessException UAE)
        {
            return Unauthorized(UAE.Message);
        }
        catch(ArgumentException AE)
        {
            if (AE.Data.Contains("Fields") && AE.Data["Fields"] is IEnumerable<string> fields)
            {
                return BadRequest(new
                {
                    message = AE.Message,
                    fields = fields
                });
            }
            if(AE.Message == "Kenteken bestaat al voor deze gebruiker")
            {
                return Conflict(AE.Message);
            }
            return BadRequest(AE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij aanmaken voertuig");
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    #endregion

    #region PATCH Endpoints

    /// <summary>
    /// PATCH /api/vehicles/{id} - Update een voertuig
    /// </summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateVehicle(int id, UpdateVehicleRequestDto request)
    {
        try
        {
            Vehicles vehicle = await _vehicleService.UpdateVehicleService(id, request, User);
            return Ok(vehicle);
        }
        catch(UnauthorizedAccessException UAE)
        {
            if(UAE.Message == "Access denied")
            {
                return Forbid();
            }
            return Unauthorized(UAE.Message);
        }
        catch(KeyNotFoundException KNFE)
        {
            return NotFound(KNFE.Message);
        }
        catch(ArgumentException AE)
        {
            return BadRequest(AE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij updaten voertuig {VehicleId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    #endregion

    #region DELETE Endpoints

    /// <summary>
    /// DELETE /api/vehicles/{id} - Verwijder een voertuig
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        try
        {
            string message = await _vehicleService.DeleteVehicleService(id, User);
            return Ok(new { message });
        }
        catch(UnauthorizedAccessException UAE)
        {
            if(UAE.Message == "Access denied")
            {
                return Forbid();
            }
            return Unauthorized(UAE.Message);
        }
        catch(KeyNotFoundException KNFE)
        {
            return NotFound(KNFE.Message);
        }
        catch(ArgumentException AE)
        {
           return BadRequest(AE.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij verwijderen voertuig {VehicleId}", id);
            return StatusCode(500, "Er is een fout opgetreden");
        }
    }

    #endregion
}