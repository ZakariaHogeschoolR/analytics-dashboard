using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MobyParkApi.Controllers;
using MobyParkApi.Services;
using MobyParkApi.Models.Dto;
using MobyParkApi.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace MobyParkApi.Tests.Controllers
{
    public class PaymentsControllerTests
    {
        private readonly Mock<PaymentService> _mockPaymentService;
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly Mock<IPaymentGenerationService> _mockPaymentGenerationService;
        private readonly PaymentsController _controller;

        public PaymentsControllerTests()
        {
            // Setup mocks
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;
            _mockContext = new Mock<ApplicationDbContext>(options);
            
            _mockPaymentService = new Mock<PaymentService>(
                _mockContext.Object,
                Mock.Of<IDiscountCodeService>(),
                Mock.Of<IArchiveService>()
            );
            
            _mockPaymentGenerationService = new Mock<IPaymentGenerationService>();

            _controller = new PaymentsController(
                _mockPaymentService.Object,
                _mockContext.Object,
                _mockPaymentGenerationService.Object
            );
        }

        #region CreatePayment Tests

        [Fact]
        public async Task CreatePayment_WithValidData_ReturnsCreatedResult()
        {
            // Arrange
            var userId = 1;
            SetupUserClaims(userId, "User");

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            var expectedPayment = new PaymentDto
            {
                Id = 1,
                LicensePlate = "AB-123-CD",
                PaymentStatus = "Pending",
                Cost = 5.00m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60)
            };

            _mockPaymentService
                .Setup(s => s.CreatePaymentAsync(userId, dto))
                .ReturnsAsync(expectedPayment);

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = result.Result as CreatedAtActionResult;
            createdResult!.Value.Should().BeEquivalentTo(expectedPayment);
            createdResult.ActionName.Should().Be(nameof(PaymentsController.GetPayment));
        }

        [Fact]
        public async Task CreatePayment_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            SetupUserClaims(1, "User");
            _controller.ModelState.AddModelError("Duration", "Duur is verplicht.");

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 0
            };

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = result.Result as BadRequestObjectResult;
            badRequest!.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task CreatePayment_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Arrange - geen claims ingesteld
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task CreatePayment_WithUnauthorizedVehicle_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "WRONG-PLATE",
                Duration = 60
            };

            _mockPaymentService
                .Setup(s => s.CreatePaymentAsync(1, dto))
                .ThrowsAsync(new UnauthorizedAccessException("Kenteken komt niet overen met uw voertuigen."));

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task CreatePayment_WithInvalidParkingLot_ReturnsBadRequest()
        {
            // Arrange
            SetupUserClaims(1, "User");

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 999,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            _mockPaymentService
                .Setup(s => s.CreatePaymentAsync(1, dto))
                .ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreatePayment_WithServiceException_ReturnsInternalServerError()
        {
            // Arrange
            SetupUserClaims(1, "User");

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            _mockPaymentService
                .Setup(s => s.CreatePaymentAsync(1, dto))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreatePayment(dto);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        #endregion

        #region UpdatePaymentStatus Tests

        [Fact]
        public async Task UpdatePaymentStatus_AsAdmin_WithValidStatus_ReturnsOk()
        {
            // Arrange
            var userId = 1;
            var paymentId = 10;
            SetupUserClaims(userId, "Admin");

            var dto = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            var expectedPayment = new PaymentDto
            {
                Id = paymentId,
                LicensePlate = "AB-123-CD",
                PaymentStatus = "Paid",
                Cost = 5.00m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60)
            };

            _mockPaymentService
                .Setup(s => s.UpdatePaymentStatusAsync(userId, "Admin", paymentId, "Paid"))
                .ReturnsAsync(expectedPayment);

            // Act
            var result = await _controller.UpdatePaymentStatus(paymentId, dto);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedPayment);
        }

        [Fact]
        public async Task UpdatePaymentStatus_AsUser_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            var dto = new UpdatePaymentStatusDto { NewStatus = "Paid" };

            _mockPaymentService
                .Setup(s => s.UpdatePaymentStatusAsync(1, "User", 10, "Paid"))
                .ThrowsAsync(new UnauthorizedAccessException("Alleen een admin mag de betaalstatus wijzigen."));

            // Act
            var result = await _controller.UpdatePaymentStatus(10, dto);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UpdatePaymentStatus_WithInvalidStatus_ReturnsBadRequest()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            var dto = new UpdatePaymentStatusDto { NewStatus = "InvalidStatus" };

            _mockPaymentService
                .Setup(s => s.UpdatePaymentStatusAsync(1, "Admin", 10, "InvalidStatus"))
                .ThrowsAsync(new ArgumentException("Ongeldige status. Gebruik: Pending, Paid of Failed."));

            // Act
            var result = await _controller.UpdatePaymentStatus(10, dto);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UpdatePaymentStatus_WithNonExistentPayment_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            var dto = new UpdatePaymentStatusDto { NewStatus = "Paid" };

            _mockPaymentService
                .Setup(s => s.UpdatePaymentStatusAsync(1, "Admin", 999, "Paid"))
                .ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.UpdatePaymentStatus(999, dto);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetPayment Tests

        [Fact]
        public async Task GetPayment_WithExistingId_ReturnsOk()
        {
            // Arrange
            var paymentId = 1;
            var expectedPayment = new PaymentDto
            {
                Id = paymentId,
                LicensePlate = "AB-123-CD",
                PaymentStatus = "Paid",
                Cost = 5.00m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60)
            };

            _mockPaymentService
                .Setup(s => s.GetPaymentAsync(paymentId))
                .ReturnsAsync(expectedPayment);

            // Act
            var result = await _controller.GetPayment(paymentId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedPayment);
        }

        [Fact]
        public async Task GetPayment_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            _mockPaymentService
                .Setup(s => s.GetPaymentAsync(999))
                .ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.GetPayment(999);

            // Assert
            result.Result.Should().BeOfType<NotFoundResult>();
        }

        #endregion

        #region GetPaymentStatus Tests

        [Fact]
        public async Task GetPaymentStatus_AsOwner_ReturnsOk()
        {
            // Arrange
            var userId = 1;
            var paymentId = 10;
            SetupUserClaims(userId, "User");

            _mockPaymentService
                .Setup(s => s.GetPaymentStatusAsync(paymentId, userId))
                .ReturnsAsync("Paid");

            // Act
            var result = await _controller.GetPaymentStatus(paymentId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var value = okResult!.Value as dynamic;
            ((string)value!.status).Should().Be("Paid");
        }

        [Fact]
        public async Task GetPaymentStatus_WithNonExistentPayment_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims(1, "User");

            _mockPaymentService
                .Setup(s => s.GetPaymentStatusAsync(999, 1))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.GetPaymentStatus(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetPaymentStatus_UnauthorizedUser_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            _mockPaymentService
                .Setup(s => s.GetPaymentStatusAsync(10, 1))
                .ThrowsAsync(new UnauthorizedAccessException("Not allowed to view this payment status"));

            // Act
            var result = await _controller.GetPaymentStatus(10);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task GetPaymentStatus_WithInvalidUserId_ReturnsUnauthorized()
        {
            // Arrange - invalid user ID claim
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "invalid"),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.GetPaymentStatus(10);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region GetUserPayments Tests

        [Fact]
        public async Task GetUserPayments_ForOwnPayments_ReturnsOk()
        {
            // Arrange
            var userId = 1;
            SetupUserClaims(userId, "User");

            var expectedPayments = new List<PaymentDto>
            {
                new PaymentDto
                {
                    Id = 1,
                    LicensePlate = "AB-123-CD",
                    PaymentStatus = "Paid",
                    Cost = 5.00m,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(60)
                }
            };

            _mockPaymentService
                .Setup(s => s.GetPaymentsByUserAsync(userId, userId))
                .ReturnsAsync(expectedPayments);

            // Act
            var result = await _controller.GetUserPayments(null);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedPayments);
        }

        [Fact]
        public async Task GetUserPayments_AsAdminForOtherUser_ReturnsOk()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            var expectedPayments = new List<PaymentDto>
            {
                new PaymentDto
                {
                    Id = 1,
                    LicensePlate = "AB-123-CD",
                    PaymentStatus = "Paid",
                    Cost = 5.00m,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(60)
                }
            };

            _mockPaymentService
                .Setup(s => s.GetPaymentsByUserAsync(2, 1))
                .ReturnsAsync(expectedPayments);

            // Act
            var result = await _controller.GetUserPayments(2);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetUserPayments_AsUserForOtherUser_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            _mockPaymentService
                .Setup(s => s.GetPaymentsByUserAsync(2, 1))
                .ThrowsAsync(new UnauthorizedAccessException("Not allowed to view payments of another user"));

            // Act
            var result = await _controller.GetUserPayments(2);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
        }

        #endregion

        #region RefundPayment Tests

        [Fact]
        public async Task RefundPayment_AsAdmin_ReturnsOk()
        {
            // Arrange
            var paymentId = 10;
            SetupUserClaims(1, "Admin");

            var expectedRefund = new PaymentDto
            {
                Id = 11,
                LicensePlate = "AB-123-CD",
                PaymentStatus = "Refund",
                Cost = -5.00m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60)
            };

            _mockPaymentService
                .Setup(s => s.RefundPaymentAsync(paymentId, 1))
                .ReturnsAsync(expectedRefund);

            // Act
            var result = await _controller.RefundPayment(paymentId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedRefund);
        }

        [Fact]
        public async Task RefundPayment_AsUser_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            // Act
            var result = await _controller.RefundPayment(10);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task RefundPayment_WithNonExistentPayment_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            _mockPaymentService
                .Setup(s => s.RefundPaymentAsync(999, 1))
                .ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.RefundPayment(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task RefundPayment_WithException_ReturnsInternalServerError()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            _mockPaymentService
                .Setup(s => s.RefundPaymentAsync(10, 1))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.RefundPayment(10);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        #endregion

        #region GetPaymentHistory Tests

        [Fact]
        public async Task GetPaymentHistory_AsUser_ReturnsOwnPayments()
        {
            // Arrange
            var userId = 1;
            SetupUserClaims(userId, "User");

            var expectedHistory = new List<PaymentDto>
            {
                new PaymentDto
                {
                    Id = 1,
                    LicensePlate = "AB-123-CD",
                    PaymentStatus = "Paid",
                    Cost = 5.00m,
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(60)
                }
            };

            _mockPaymentService
                .Setup(s => s.GetPaymentHistoryAsync(userId, "User"))
                .ReturnsAsync(expectedHistory);

            // Act
            var result = await _controller.GetPaymentHistory();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedHistory);
        }

        [Fact]
        public async Task GetPaymentHistory_AsAdmin_ReturnsAllPayments()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            var expectedHistory = new List<PaymentDto>
            {
                new PaymentDto
                {
                    Id = 1,
                    LicensePlate = "AB-123-CD",
                    PaymentStatus = "Paid",
                    Cost = 5.00m,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(60)
                },
                new PaymentDto
                {
                    Id = 2,
                    LicensePlate = "XY-789-ZZ",
                    PaymentStatus = "Pending",
                    Cost = 10.00m,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(120)
                }
            };

            _mockPaymentService
                .Setup(s => s.GetPaymentHistoryAsync(1, "Admin"))
                .ReturnsAsync(expectedHistory);

            // Act
            var result = await _controller.GetPaymentHistory();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var resultList = okResult!.Value as List<PaymentDto>;
            resultList.Should().HaveCount(2);
        }

        #endregion

        #region GetUserTotal Tests

        [Fact]
        public async Task GetUserTotal_ReturnsCorrectTotal()
        {
            // Arrange
            var userId = 1;
            SetupUserClaims(userId, "User");

            var expectedResult = new
            {
                userId = 1,
                transactionCount = 5,
                total = 25.50m
            };

            _mockPaymentService
                .Setup(s => s.CalculateUserTotalAsync(userId))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GetUserTotal();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public async Task GetUserTotal_WithInvalidUserId_ReturnsUnauthorized()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, ""),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.GetUserTotal();

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region GetUserTotalForAdmin Tests

        [Fact]
        public async Task GetUserTotalForAdmin_AsAdmin_ReturnsTotal()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            var expectedResult = new
            {
                userId = 2,
                transactionCount = 3,
                total = 15.00m
            };

            _mockPaymentService
                .Setup(s => s.CalculateAdminTotalAsync(2))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GetUserTotalForAdmin(2);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public async Task GetUserTotalForAdmin_AsUser_ReturnsForbidden()
        {
            // Arrange
            SetupUserClaims(1, "User");

            // Act
            var result = await _controller.GetUserTotalForAdmin(2);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task GetUserTotalForAdmin_WithNonExistentUser_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            _mockPaymentService
                .Setup(s => s.CalculateAdminTotalAsync(999))
                .ThrowsAsync(new KeyNotFoundException("User with ID 999 not found."));

            // Act
            var result = await _controller.GetUserTotalForAdmin(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region CreatePaymentFromParkingSession Tests

        [Fact]
        public async Task CreatePaymentFromParkingSession_WithValidSession_ReturnsCreated()
        {
            // Arrange
            var sessionId = 1;
            var paymentId = 10;

            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromParkingSessionAsync(sessionId))
                .ReturnsAsync(paymentId);

            // Act
            var result = await _controller.CreatePaymentFromParkingSession(sessionId);

            // Assert
            result.Should().BeOfType<CreatedResult>();
            var createdResult = result as CreatedResult;
            createdResult!.Location.Should().Be($"/api/payments/{paymentId}");
        }

        [Fact]
        public async Task CreatePaymentFromParkingSession_WithNonExistentSession_ReturnsNotFound()
        {
            // Arrange
            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromParkingSessionAsync(999))
                .ThrowsAsync(new KeyNotFoundException("Session not found"));

            // Act
            var result = await _controller.CreatePaymentFromParkingSession(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CreatePaymentFromParkingSession_WithInvalidOperation_ReturnsBadRequest()
        {
            // Arrange
            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromParkingSessionAsync(1))
                .ThrowsAsync(new InvalidOperationException("Session not closed"));

            // Act
            var result = await _controller.CreatePaymentFromParkingSession(1);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region CreatePaymentFromReservation Tests

        [Fact]
        public async Task CreatePaymentFromReservation_WithValidReservation_ReturnsCreated()
        {
            // Arrange
            var reservationId = 1;
            var paymentId = 10;

            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromReservationAsync(reservationId))
                .ReturnsAsync(paymentId);

            // Act
            var result = await _controller.CreatePaymentFromReservation(reservationId);

            // Assert
            result.Should().BeOfType<CreatedResult>();
            var createdResult = result as CreatedResult;
            createdResult!.Location.Should().Be($"/api/payments/{paymentId}");
        }

        [Fact]
        public async Task CreatePaymentFromReservation_WithNonExistentReservation_ReturnsNotFound()
        {
            // Arrange
            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromReservationAsync(999))
                .ThrowsAsync(new KeyNotFoundException("Reservation not found"));

            // Act
            var result = await _controller.CreatePaymentFromReservation(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CreatePaymentFromReservation_WithInvalidOperation_ReturnsBadRequest()
        {
            // Arrange
            _mockPaymentGenerationService
                .Setup(s => s.CreatePaymentFromReservationAsync(1))
                .ThrowsAsync(new InvalidOperationException("Reservation not completed"));

            // Act
            var result = await _controller.CreatePaymentFromReservation(1);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region Helper Methods

        private void SetupUserClaims(int userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #endregion
    }
}