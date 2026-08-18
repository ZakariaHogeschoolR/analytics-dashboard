using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using MobyParkApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MobyParkApi.Controllers;

[ApiController]
[SwaggerOrder(7)]
[Route("api/[controller]")]
[Authorize]
public class DiscountCodeController : ControllerBase
{
    private readonly IDiscountCodeService _discountCodeService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiscountCodeController> _logger;

    public DiscountCodeController(
        IDiscountCodeService discountCodeService,
        ApplicationDbContext context,
        ILogger<DiscountCodeController> logger)
    {
        _discountCodeService = discountCodeService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/DiscountCode - Maak een nieuwe kortingscode aan (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDiscountCode([FromBody] CreateDiscountCodeDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (userId == 0)
            {
                return Unauthorized("Gebruiker niet ingelogd");
            }

            _logger.LogInformation("Aanmaken kortingscode {Code} door admin {UserId}", dto.Code, userId);

            var discountCode = await _discountCodeService.CreateDiscountCodeAsync(dto, userId);

            return CreatedAtAction(
                nameof(GetDiscountCodeById),
                new { id = discountCode.Id },
                discountCode
            );
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validatiefout bij aanmaken kortingscode");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij aanmaken kortingscode");
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het aanmaken van de kortingscode" });
        }
    }

    /// <summary>
    /// GET /api/DiscountCode/{id} - Haal een kortingscode op (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDiscountCodeById(int id)
    {
        try
        {
            var discountCode = await _discountCodeService.GetDiscountCodeByIdAsync(id);

            if (discountCode == null)
            {
                return NotFound(new { error = "Kortingscode niet gevonden" });
            }

            return Ok(discountCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen kortingscode {Id}", id);
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het ophalen van de kortingscode" });
        }
    }

    /// <summary>
    /// GET /api/DiscountCode - Haal alle kortingscodes op (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDiscountCodes([FromQuery] bool? activeOnly = null)
    {
        try
        {
            var discountCodes = await _discountCodeService.GetAllDiscountCodesAsync(activeOnly);
            return Ok(discountCodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen kortingscodes");
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het ophalen van de kortingscodes" });
        }
    }

    /// <summary>
    /// PUT /api/DiscountCode/{id} - Update een kortingscode (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDiscountCode(int id, [FromBody] UpdateDiscountCodeDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Updaten kortingscode {Id}", id);

            var discountCode = await _discountCodeService.UpdateDiscountCodeAsync(id, dto);

            return Ok(discountCode);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Kortingscode {Id} niet gevonden voor update", id);
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validatiefout bij updaten kortingscode {Id}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij updaten kortingscode {Id}", id);
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het updaten van de kortingscode" });
        }
    }

    /// <summary>
    /// DELETE /api/DiscountCode/{id} - Deactiveer een kortingscode (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateDiscountCode(int id)
    {
        try
        {
            _logger.LogInformation("Deactiveren kortingscode {Id}", id);

            await _discountCodeService.DeactivateDiscountCodeAsync(id);

            return Ok(new { message = "Kortingscode succesvol gedeactiveerd", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Kortingscode {Id} niet gevonden voor deactiveren", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij deactiveren kortingscode {Id}", id);
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het deactiveren van de kortingscode" });
        }
    }

    /// <summary>
    /// POST /api/DiscountCode/validate - Valideer een kortingscode (User/Admin)
    /// </summary>
    [HttpPost("validate")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> ValidateDiscountCode([FromBody] ValidateDiscountCodeDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (userId == 0)
            {
                return Unauthorized("Gebruiker niet ingelogd");
            }

            _logger.LogDebug("Valideren kortingscode {Code} voor user {UserId}", dto.Code, userId);

            var result = await _discountCodeService.ValidateDiscountCodeAsync(
                dto.Code,
                userId,
                dto.ParkingLotId,
                dto.ReservationStartTime,
                dto.OriginalCost ?? 0
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij valideren kortingscode");
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het valideren van de kortingscode" });
        }
    }

    /// <summary>
    /// GET /api/DiscountCode/{id}/statistics - Haal statistieken op voor een kortingscode (Admin only)
    /// </summary>
    [HttpGet("{id}/statistics")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDiscountCodeStatistics(int id)
    {
        try
        {
            var statistics = await _discountCodeService.GetDiscountCodeStatisticsAsync(id);
            return Ok(statistics);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Kortingscode {Id} niet gevonden voor statistieken", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij ophalen statistieken voor kortingscode {Id}", id);
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het ophalen van de statistieken" });
        }
    }
}

