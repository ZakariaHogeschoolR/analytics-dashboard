using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MobyParkApi.Services;
using MobyParkApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MobyParkApi.Controllers;

[SwaggerOrder(9)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArchiveController : ControllerBase
{
    private readonly IArchiveService _archiveService;
    private readonly IInvoiceArchiveService _invoiceArchiveService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ArchiveController> _logger;

    public ArchiveController(
        IArchiveService archiveService, 
        IInvoiceArchiveService invoiceArchiveService,
        ApplicationDbContext context,
        ILogger<ArchiveController> logger)
    {
        _archiveService = archiveService;
        _invoiceArchiveService = invoiceArchiveService;
        _context = context;
        _logger = logger;
    }

    // ==========================================================================
    // RESERVATIONS ARCHIVING
    // ==========================================================================

    /// <summary>
    /// POST /api/archive/reservations - Archiveert alle afgeronde reserveringen (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reservations")]
    public async Task<IActionResult> ArchiveReservations()
    {
        try
        {
            _logger.LogInformation("Archiveer reserveringen endpoint aangeroepen");

            var archivedCount = await _archiveService.ArchiveReservationsAsync();

            _logger.LogInformation("Archiveer reserveringen voltooid: {Count} reserveringen gearchiveerd", archivedCount);

            return Ok(new
            {
                message = "Reserveringen succesvol gearchiveerd",
                archivedCount = archivedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout bij archiveren van reserveringen");
            return StatusCode(500, new { error = "Er is een fout opgetreden bij het archiveren van reserveringen" });
        }
    }

    [HttpDelete("payments/{id}")]
    // Alleen admin toestaan
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        try 
        {
            // Gebruiker met id ophalen
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // betaling zoeken  
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var adminId))
                return BadRequest("Geen betaling gevonden onder id");

            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
                return NotFound(new { error = "Betaling niet gevonden" });

            var (success, errorMessage) = await _archiveService.ArchiveAndDeletePaymentAsync(payment, "Admin", adminId);

            if (!success)
                return BadRequest(new { error = errorMessage });

            return Ok(new { message = $"Betaling {id} is succesvol verwijderd (gearchiveerd)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting payment {paymentId}", id);
            return StatusCode(500, new { message = "Error bij verwijderen betaling", error = ex.Message });
        }
    }

    // ==========================================================================
    // INVOICES ARCHIVING
    // ==========================================================================

    /// <summary>
    /// GET /api/archive/invoices - Haal alle gearchiveerde invoices op voor ingelogde gebruiker
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetArchivedInvoices()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            _logger.LogInformation("Fetching archived invoices for user {UserId}", userId.Value);

            var archivedInvoices = await _invoiceArchiveService.GetArchivedInvoicesForUserAsync(userId.Value);

            return Ok(archivedInvoices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching archived invoices");
            return StatusCode(500, new { error = "Error fetching archived invoices" });
        }
    }

    /// <summary>
    /// GET /api/archive/invoices/{id} - Haal specifieke gearchiveerde invoice op
    /// </summary>
    [HttpGet("invoices/{id}")]
    public async Task<IActionResult> GetArchivedInvoice(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            _logger.LogInformation("Fetching archived invoice {ArchiveId} for user {UserId}", id, userId.Value);

            var archivedInvoice = await _invoiceArchiveService.GetArchivedInvoiceAsync(id);

            if (archivedInvoice == null)
            {
                return NotFound(new { error = "Archived invoice not found" });
            }

            // Check if invoice belongs to user
            if (archivedInvoice.UserId != userId.Value)
            {
                _logger.LogWarning("User {UserId} attempted to access archived invoice {ArchiveId} belonging to user {OwnerId}",
                    userId.Value, id, archivedInvoice.UserId);
                return Forbid();
            }

            return Ok(archivedInvoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching archived invoice {ArchiveId}", id);
            return StatusCode(500, new { error = "Error fetching archived invoice" });
        }
    }

    /// <summary>
    /// POST /api/archive/invoices - Archiveer alle betaalde invoices (Admin only)
    /// </summary>
    [HttpPost("invoices")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveAllPaidInvoices()
    {
        try
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrEmpty(username))
            {
                username = "Admin";
            }

            _logger.LogInformation("Starting manual batch archiving of all paid invoices by {Username}", username);

            var archivedCount = await _invoiceArchiveService.ArchiveAllPaidInvoicesAsync(username);

            return Ok(new
            {
                success = true,
                message = $"Successfully archived {archivedCount} paid invoices",
                archivedCount = archivedCount,
                archivedBy = username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch archiving");
            return StatusCode(500, new { error = "Error archiving invoices" });
        }
    }

    /// <summary>
    /// POST /api/archive/invoices/{invoiceId} - Archiveer een specifieke betaalde invoice (Admin only)
    /// </summary>
    [HttpPost("invoices/{invoiceId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveSpecificInvoice(int invoiceId)
    {
        try
        {
            var username = await GetCurrentUsernameAsync();
            if (string.IsNullOrEmpty(username))
            {
                username = "Admin";
            }

            _logger.LogInformation("Manual archiving of invoice {InvoiceId} by {Username}", invoiceId, username);

            var wasArchived = await _invoiceArchiveService.ArchiveInvoiceAsync(invoiceId, username);

            if (!wasArchived)
            {
                return BadRequest(new { error = "Invoice could not be archived. It may not exist or not be paid." });
            }

            return Ok(new
            {
                success = true,
                message = $"Invoice {invoiceId} successfully archived",
                invoiceId = invoiceId,
                archivedBy = username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { error = "Error archiving invoice" });
        }
    }

    // ==========================================================================
    // HELPER METHODS
    // ==========================================================================

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                       ?? User.FindFirst("sub")
                       ?? User.FindFirst("userId");

        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }

        return null;
    }

    private async Task<string?> GetCurrentUsernameAsync()
    {
        var principal = HttpContext?.User;
        var username = principal?.Identity?.Name;
        
        if (!string.IsNullOrEmpty(username))
        {
            return username;
        }

        // Fallback: haal username op via userId
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            var user = await _context.Users.FindAsync(userId.Value);
            return user?.Username;
        }

        return null;
    }

    
}