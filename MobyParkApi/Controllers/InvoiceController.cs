namespace MobyParkApi.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MobyParkApi.Services;
using MobyParkApi.DTOs;

[SwaggerOrder(8)]
[ApiController]
[Route("api/[controller]")]
[Authorize] // Alleen ingelogde gebruikers
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(IInvoiceService invoiceService, ILogger<InvoiceController> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/invoice/invoices - Haalt alle facturen op voor de ingelogde gebruiker
    /// </summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(List<InvoiceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<InvoiceListItemDto>>> GetInvoices()
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            _logger.LogWarning("Unable to get user ID from claims");
            return Unauthorized("User ID not found in token");
        }

        _logger.LogInformation("Getting invoices for user {UserId}", userId);

        var invoices = await _invoiceService.GetInvoicesByUserIdAsync(userId.Value);
        return Ok(invoices);
    }

    /// <summary>
    /// GET /api/invoice/invoices/{id} - Haalt details op van een specifieke factuur
    /// </summary>
    [HttpGet("invoices/{id}")]
    [ProducesResponseType(typeof(InvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDetailDto>> GetInvoiceById(int id)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            _logger.LogWarning("Unable to get user ID from claims");
            return Unauthorized("User ID not found in token");
        }

        _logger.LogInformation("Getting invoice {InvoiceId} for user {UserId}", id, userId);

        var invoice = await _invoiceService.GetInvoiceByIdAsync(id, userId.Value);
        
        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for user {UserId}", id, userId);
            return NotFound($"Invoice {id} not found");
        }

        return Ok(invoice);
    }

    /// <summary>
    /// POST /api/invoice/invoices/{id}/pay - Verwerkt betaling van een factuur
    /// </summary>
    [HttpPost("invoices/{id}/pay")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponseDto>> PayInvoice(int id, [FromBody] PayInvoiceDto paymentDto)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            _logger.LogWarning("Unable to get user ID from claims");
            return Unauthorized("User ID not found in token");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Processing payment for invoice {InvoiceId} by user {UserId}", id, userId);

        try
        {
            var result = await _invoiceService.PayInvoiceAsync(id, userId.Value, paymentDto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Invoice {InvoiceId} not found for user {UserId}", id, userId);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation on invoice {InvoiceId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for invoice {InvoiceId}", id);
            return StatusCode(500, "An error occurred while processing the payment");
        }
    }

    /// <summary>
    /// POST /api/invoice/generate-invoices - Genereert maandelijkse facturen voor alle gebruikers
    /// </summary>
    [HttpPost("generate-invoices")]
    [ProducesResponseType(typeof(GenerateInvoicesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateInvoicesResultDto>> GenerateInvoices([FromQuery] int year, [FromQuery] int month)
    {
        // TODO: Voeg [Authorize(Roles = "Admin")] toe als je role-based authorization hebt
        // Voor nu kan iedereen dit endpoint aanroepen (handig voor testen)

        if (year < 2000 || year > 2100)
        {
            return BadRequest("Invalid year. Must be between 2000 and 2100");
        }

        if (month < 1 || month > 12)
        {
            return BadRequest("Invalid month. Must be between 1 and 12");
        }

        _logger.LogInformation("Generating invoices for {Year}-{Month}", year, month);

        try
        {
            var result = await _invoiceService.GenerateMonthlyInvoicesAsync(year, month);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for invoice generation");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoices for {Year}-{Month}", year, month);
            return StatusCode(500, "An error occurred while generating invoices");
        }
    }

    /// <summary>
    /// Helper method om user ID uit JWT claims te halen
    /// </summary>
    private int? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("userId")?.Value;

        if (int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }

        return null;
    }
}