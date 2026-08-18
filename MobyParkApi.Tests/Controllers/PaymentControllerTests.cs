using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Moq;
using Xunit;
using System.Text.Json;
using Moq;

namespace MobyParkApi.Tests
{
    public class PaymentsControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly PaymentService _service;
        private readonly PaymentsController _controller;
        private readonly Mock<IPaymentGenerationService> _paymentGenerationServiceMock;

        public PaymentsControllerTests()
        {
            // Setup InMemory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            var mockDiscountCodeService = new Mock<IDiscountCodeService>();
            var mockArchiveService = new Mock<IArchiveService>();
            _service = new PaymentService(_context, mockDiscountCodeService.Object, mockArchiveService.Object);

            SeedTestData();

            // Default mock user (Owner, ID = 1)
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "User")
            }));


            var mockPaymentGenerationService = new Mock<IPaymentGenerationService>();
            _controller = new PaymentsController(_service, _context, mockPaymentGenerationService.Object)

            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
        }

        private void SeedTestData()
        {
            var parkingLot = new ParkingLots
            {
                Id = 1,
                Name = "Test Parking Lot",
                Tariff = 5.00m,
                Capacity = 100,
                Location = "Test Location",
                Address = "Test Address",
                Coordinates = "0,0"
            };

            _context.ParkingLots.Add(parkingLot);

            var ownerPayment = new Payments
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AA-123-AA",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            var otherPayment = new Payments
            {
                Id = 2,
                UserId = 2,
                ParkingLotId = 1,
                LicensePlate = "BB-456-BB",
                Duration = 120,
                PaymentStatus = "Pending",
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow,
                Cost = 10.00m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            _context.Payments.AddRange(ownerPayment, otherPayment);

            _context.Users.AddRange(
                new Users { Id = 1, Username = "Owner", Password = "pw", Email = "a@a.nl", Phone_Number = "123", Role = "User", Name = "Owner" },
                new Users { Id = 2, Username = "Other", Password = "pw", Email = "b@b.nl", Phone_Number = "456", Role = "User", Name = "Other" },
                new Users { Id = 3, Username = "Admin", Password = "pw", Email = "c@c.nl", Phone_Number = "789", Role = "Admin", Name = "Admin" }
            );

        // Ensure user 1 has a vehicle with license plate used in tests (geldig Nederlands formaat)
        _context.Vehicles.Add(new Vehicles {
            Id = 1,
            LicensePlate = "AB-123-C",  // Geldig Nederlands kenteken formaat: XX-999-X
            Make = "Toyota",
            Model = "Corolla",
            Color = "Blue",
            Year = 2020,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });

            _context.SaveChanges();
        }

        // ────────────────────────────────
        //  CREATE PAYMENT TESTS
        // ────────────────────────────────

        [Fact(DisplayName = "CreatePayment: geldig verzoek retourneert CreatedAtActionResult")]
        public async Task CreatePayment_ValidInput_ReturnsCreatedPayment()
        {
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-C",  // Geldig Nederlands kenteken
                Duration = 120
            };

            var result = await _controller.CreatePayment(dto);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDto>(createdResult.Value);

            Assert.Equal("AB-123-C", returnValue.LicensePlate);
            Assert.Equal("Pending", returnValue.PaymentStatus);
        }

        [Fact(DisplayName = "CreatePayment: ongeldig parkeervak retourneert BadRequest")]
        public async Task CreatePayment_InvalidParkingLot_ReturnsBadRequest()
        {
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 999,
                LicensePlate = "AB-123-CD",
                Duration = 120
            };

            var result = await _controller.CreatePayment(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("parkeer", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        // ────────────────────────────────
        //  STATUS ENDPOINT TESTS
        // ────────────────────────────────

        [Fact(DisplayName = "GetPaymentStatus: eigenaar krijgt status OK + Paid")]
        public async Task GetPaymentStatus_AsOwner_ReturnsOkWithStatus()
        {
            var result = await _controller.GetPaymentStatus(1);

            var okResult = Assert.IsType<OkObjectResult>(result);
            string json = JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("\"Paid\"", json);
        }

        [Fact(DisplayName = "GetPaymentStatus: andere gebruiker krijgt 403")]
        public async Task GetPaymentStatus_AsOtherUser_ReturnsForbidden()
        {
            var otherUser = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "2"),
                new Claim(ClaimTypes.Role, "User")
            }));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = otherUser }
            };

            var result = await _controller.GetPaymentStatus(1);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        }

        [Fact(DisplayName = "GetPaymentStatus: admin kan alle betalingen zien")]
        public async Task GetPaymentStatus_AsAdmin_CanAccessAllPayments()
        {
            var admin = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "3"),
                new Claim(ClaimTypes.Role, "Admin")
            }));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = admin }
            };

            var result = await _controller.GetPaymentStatus(2);

            var okResult = Assert.IsType<OkObjectResult>(result);
            string json = JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("\"Pending\"", json);
        }

        [Fact(DisplayName = "GetPaymentStatus: onbekende betaling retourneert NotFound + message")]
        public async Task GetPaymentStatus_NonExistingPayment_ReturnsNotFound()
        {
            var result = await _controller.GetPaymentStatus(999);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            string json = JsonSerializer.Serialize(notFoundResult.Value);
            Assert.Contains("\"Payment not found\"", json);
        }

        // ────────────────────────────────
        //  CLEANUP
        // ────────────────────────────────
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
