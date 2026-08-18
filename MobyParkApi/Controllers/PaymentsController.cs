using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MobyParkApi.Services;
using MobyParkApi.Models.Dto;
using MobyParkApi.Models;
using System.Security.Claims;
using MobyParkApi.Data; 

namespace MobyParkApi.Controllers
{
    [ApiController]
    [SwaggerOrder(6)]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly PaymentService _paymentService;
        private readonly ApplicationDbContext _context;
        private readonly IPaymentGenerationService _paymentGenerationService;

        public PaymentsController(
            PaymentService paymentService, 
            ApplicationDbContext context,
            IPaymentGenerationService paymentGenerationService)
        {
            _paymentService = paymentService;
            _context = context;
            _paymentGenerationService = paymentGenerationService;
        }

        [HttpPost]
        public async Task<ActionResult<PaymentDto>> CreatePayment(CreatedPaymentDto dto)
        {
            // ASP.NET modelvalidatie controle
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "Ongeldige invoer", details = errors });
            }

            // Auth user ophalen (of test fallback)
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (userId == 0)
                return Unauthorized("Gebruiker niet ingelogd of token ongeldig.");

            try
            {
                var payment = await _paymentService.CreatePaymentAsync(userId, dto);

                if (payment == null)
                    return BadRequest("Geen geldige parkeerplaats gevonden of berekening mislukt.");

                return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
            }
            catch (UnauthorizedAccessException ex) 
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Payment creation failed: {ex.Message}");
                return StatusCode(500, "Er is een interne fout opgetreden bij het aanmaken van de betaling.");
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<ActionResult<PaymentDto>> UpdatePaymentStatus(int id, UpdatePaymentStatusDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
            if (userId == 0)
                return Unauthorized("Gebruiker niet ingelogd.");

            try
            {
                var updatedPayment = await _paymentService.UpdatePaymentStatusAsync(userId, userRole, id, dto.NewStatus);

                if (updatedPayment == null)
                    return NotFound("Betaling niet gevonden.");

                return Ok(updatedPayment);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Status update failed: {ex.Message}");
                return StatusCode(500, "Er is een interne fout opgetreden.");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDto>> GetPayment(int id)
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            return payment == null ? NotFound() : Ok(payment);
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetPaymentStatus(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { message = "Invalid or missing user ID" });

            try
            {
                string? paymentStatus = await _paymentService.GetPaymentStatusAsync(id, userId);

                if (paymentStatus == null)
                    return NotFound(new { message = "Payment not found" });

                return Ok(new { status = paymentStatus });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error" });
            }
        }

        [HttpGet("user")]
        public async Task<IActionResult> GetUserPayments([FromQuery] int? userId = null)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            try
            {
                int requestedUserId = userId ?? currentUserId;
                var payments = await _paymentService.GetPaymentsByUserAsync(requestedUserId, currentUserId);
                return Ok(payments);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpPost("{id}/refund")]
        public async Task<IActionResult> RefundPayment(int id)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            if (userRole != "Admin")
                return StatusCode(403, new { message = "Only admins can process refunds." });

            try
            {
                var refund = await _paymentService.RefundPaymentAsync(id, userId);

                if (refund == null)
                    return NotFound(new { message = "Payment not found or already refunded." });

                return Ok(refund);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Refund failed: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error during refund." });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { message = "Invalid or missing user ID" });

            try
            {
                var history = await _paymentService.GetPaymentHistoryAsync(userId, role);
                return Ok(history);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] History retrieval failed: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error" });
            }
        }

        [HttpGet("total")]
        public async Task<IActionResult> GetUserTotal()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { message = "Invalid or missing user ID" });

            try
            {
                var result = await _paymentService.CalculateUserTotalAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Total calculation failed: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error" });
            }
        }

        [HttpGet("admin/total")]
        public async Task<IActionResult> GetUserTotalForAdmin([FromQuery] int? userId = null)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
            if (role != "Admin")
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only admins can access this endpoint." });

            try
            {
                var result = await _paymentService.CalculateAdminTotalAsync(userId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Admin total failed: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error" });
            }
        }

        // ========== NIEUWE ENDPOINTS VOOR PAYMENT GENERATION ==========

        /// <summary>
        /// POST /api/payments/from-parking-session/{sessionId}
        /// Maakt een payment aan vanuit een afgesloten parking session
        /// </summary>
        [HttpPost("from-parking-session/{sessionId}")]
        public async Task<IActionResult> CreatePaymentFromParkingSession(int sessionId)
        {
            try
            {
                var paymentId = await _paymentGenerationService.CreatePaymentFromParkingSessionAsync(sessionId);

                return Created($"/api/payments/{paymentId}", new
                {
                    success = true,
                    paymentId = paymentId,
                    message = "Payment created successfully from parking session"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Payment from session failed: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while creating the payment" });
            }
        }

        /// <summary>
        /// POST /api/payments/from-reservation/{reservationId}
        /// Maakt een payment aan vanuit een afgeronde reservation
        /// </summary>
        [HttpPost("from-reservation/{reservationId}")]
        public async Task<IActionResult> CreatePaymentFromReservation(int reservationId)
        {
            try
            {
                var paymentId = await _paymentGenerationService.CreatePaymentFromReservationAsync(reservationId);

                return Created($"/api/payments/{paymentId}", new
                {
                    success = true,
                    paymentId = paymentId,
                    message = "Payment created successfully from reservation"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Payment from reservation failed: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while creating the payment" });
            }
        }

        // ========== HELPER METHODS (bestaande code) ==========

        private decimal CalculateCost(int parkingLotId, int duration)
        {
            var parkingLot = _context.ParkingLots.Find(parkingLotId);
            if (parkingLot == null) 
                return 0m; 

            decimal hours = (decimal)Math.Ceiling(duration / 60.0);
            return parkingLot.Tariff * hours;
        }

        private PaymentDto MapToDto(Payments payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,    
                LicensePlate = payment.LicensePlate,
                PaymentStatus = payment.PaymentStatus,
                Cost = payment.Cost,
                StartTime = payment.StartTime,
                EndTime = payment.EndTime
            };
        }
    }
}