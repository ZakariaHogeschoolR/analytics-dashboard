using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Services;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.Services
{
    public class PaymentServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly Mock<IArchiveService> _mockArchiveService;
        private readonly PaymentService _service;

        public PaymentServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _mockArchiveService = new Mock<IArchiveService>();

            _service = new PaymentService(
                _context,
                _mockDiscountCodeService.Object,
                _mockArchiveService.Object
            );

            SeedTestData();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedTestData()
        {
            // Add test users
            var users = new[]
            {
                new Users { Id = 1, Email = "user1@test.com", Role = "User", FirstName = "John", LastName = "Doe" },
                new Users { Id = 2, Email = "user2@test.com", Role = "User", FirstName = "Jane", LastName = "Smith" },
                new Users { Id = 3, Email = "admin@test.com", Role = "Admin", FirstName = "Admin", LastName = "User" }
            };
            _context.Users.AddRange(users);

            // Add test parking lots
            var parkingLots = new[]
            {
                new ParkingLots { Id = 1, Name = "Lot A", Location = "Center", Address = "Main St 1", Capacity = 100, Reserved = 0, Tariff = 2.50m, DayTariff = 20.00m },
                new ParkingLots { Id = 2, Name = "Lot B", Location = "North", Address = "North St 5", Capacity = 50, Reserved = 0, Tariff = 3.00m, DayTariff = 25.00m }
            };
            _context.ParkingLots.AddRange(parkingLots);

            // Add test vehicles
            var vehicles = new[]
            {
                new Vehicles { Id = 1, UserId = 1, LicensePlate = "AB-123-CD", Brand = "Toyota", Model = "Corolla", CreatedAt = DateTime.UtcNow },
                new Vehicles { Id = 2, UserId = 2, LicensePlate = "XY-789-ZZ", Brand = "Honda", Model = "Civic", CreatedAt = DateTime.UtcNow }
            };
            _context.Vehicles.AddRange(vehicles);

            // Add test payments
            var payments = new[]
            {
                new Payments
                {
                    Id = 1,
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 60,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(60),
                    Cost = 2.50m,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    ModifiedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Payments
                {
                    Id = 2,
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 120,
                    PaymentStatus = "Pending",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(120),
                    Cost = 5.00m,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            };
            _context.Payments.AddRange(payments);

            _context.SaveChanges();
        }

        #region CreatePaymentAsync Tests

        [Fact]
        public async Task CreatePaymentAsync_WithValidData_CreatesPayment()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.LicensePlate.Should().Be("AB-123-CD");
            result.PaymentStatus.Should().Be("Pending");
            result.Cost.Should().Be(2.50m); // 1 hour * 2.50 tariff
            
            var payment = await _context.Payments.FindAsync(result.Id);
            payment.Should().NotBeNull();
            payment!.Duration.Should().Be(60);
        }

        [Fact]
        public async Task CreatePaymentAsync_WithZeroDuration_ReturnsNull()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 0
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreatePaymentAsync_WithNegativeDuration_ReturnsNull()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = -30
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreatePaymentAsync_WithEmptyLicensePlate_ReturnsNull()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "",
                Duration = 60
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreatePaymentAsync_WithUnauthorizedVehicle_ThrowsUnauthorizedException()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "WRONG-PLATE",
                Duration = 60
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.CreatePaymentAsync(1, dto)
            );
            exception.Message.Should().Contain("Kenteken komt niet overen met uw voertuigen");
        }

        [Fact]
        public async Task CreatePaymentAsync_WithOtherUsersVehicle_ThrowsUnauthorizedException()
        {
            // Arrange - User 1 probeert vehicle van User 2 te gebruiken
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "XY-789-ZZ",
                Duration = 60
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.CreatePaymentAsync(1, dto)
            );
            exception.Message.Should().Contain("Kenteken komt niet overen met uw voertuigen");
        }

        [Fact]
        public async Task CreatePaymentAsync_WithNonExistentParkingLot_ReturnsNull()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 999,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreatePaymentAsync_CalculatesCostCorrectly_ForOneHour()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1, // Tariff = 2.50
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.Cost.Should().Be(2.50m); // 1 hour * 2.50
        }

        [Fact]
        public async Task CreatePaymentAsync_CalculatesCostCorrectly_ForTwoHours()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1, // Tariff = 2.50
                LicensePlate = "AB-123-CD",
                Duration = 120
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.Cost.Should().Be(5.00m); // 2 hours * 2.50
        }

        [Fact]
        public async Task CreatePaymentAsync_RoundsUpPartialHours()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1, // Tariff = 2.50
                LicensePlate = "AB-123-CD",
                Duration = 65 // 1 hour and 5 minutes -> should round to 2 hours
            };

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.Cost.Should().Be(5.00m); // Ceil(65/60) = 2 hours * 2.50
        }

        [Fact]
        public async Task CreatePaymentAsync_WithDiscountCode_AppliesDiscount()
        {
            // Arrange
            var discountCode = new DiscountCodes
            {
                Id = 1,
                Code = "SAVE10",
                DiscountValue = 1.00m,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            };
            _context.DiscountCodes.Add(discountCode);
            await _context.SaveChangesAsync();

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                DiscountCode = "SAVE10"
            };

            _mockDiscountCodeService
                .Setup(s => s.ApplyDiscountCodeAsync(
                    "SAVE10",
                    1,
                    1,
                    It.IsAny<DateTime>(),
                    2.50m,
                    null,
                    null
                ))
                .ReturnsAsync(1.00m);

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.Cost.Should().Be(1.50m); // 2.50 - 1.00 discount
            result.LicensePlate.Should().Be("AB-123-CD");
            
            var payment = await _context.Payments.FindAsync(result.Id);
            payment!.Discount.Should().Be(1.00m);
            payment.DiscountCodeId.Should().Be(1);
        }

        [Fact]
        public async Task CreatePaymentAsync_WithInvalidDiscountCode_ThrowsArgumentException()
        {
            // Arrange
            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                DiscountCode = "INVALID"
            };

            _mockDiscountCodeService
                .Setup(s => s.ApplyDiscountCodeAsync(
                    "INVALID",
                    1,
                    1,
                    It.IsAny<DateTime>(),
                    2.50m,
                    null,
                    null
                ))
                .ThrowsAsync(new Exception("Code not found"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreatePaymentAsync(1, dto)
            );
            exception.Message.Should().Contain("Kortingscode is niet geldig");
        }

        [Fact]
        public async Task CreatePaymentAsync_WithDiscountGreaterThanCost_SetsCostToZero()
        {
            // Arrange
            var discountCode = new DiscountCodes
            {
                Id = 1,
                Code = "FREE",
                DiscountValue = 10.00m,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            };
            _context.DiscountCodes.Add(discountCode);
            await _context.SaveChangesAsync();

            var dto = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                DiscountCode = "FREE"
            };

            _mockDiscountCodeService
                .Setup(s => s.ApplyDiscountCodeAsync(
                    "FREE",
                    1,
                    1,
                    It.IsAny<DateTime>(),
                    2.50m,
                    null,
                    null
                ))
                .ReturnsAsync(10.00m);

            // Act
            var result = await _service.CreatePaymentAsync(1, dto);

            // Assert
            result.Should().NotBeNull();
            result!.Cost.Should().Be(0m); // Max(0, 2.50 - 10.00) = 0
        }

        #endregion

        #region GetPaymentAsync Tests

        [Fact]
        public async Task GetPaymentAsync_WithExistingId_ReturnsPayment()
        {
            // Act
            var result = await _service.GetPaymentAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.LicensePlate.Should().Be("AB-123-CD");
            result.PaymentStatus.Should().Be("Paid");
        }

        [Fact]
        public async Task GetPaymentAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var result = await _service.GetPaymentAsync(999);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetPaymentStatusAsync Tests

        [Fact]
        public async Task GetPaymentStatusAsync_AsOwner_ReturnsStatus()
        {
            // Act
            var result = await _service.GetPaymentStatusAsync(1, 1);

            // Assert
            result.Should().Be("Paid");
        }

        [Fact]
        public async Task GetPaymentStatusAsync_AsAdmin_ReturnsStatus()
        {
            // Act
            var result = await _service.GetPaymentStatusAsync(1, 3); // Admin user

            // Assert
            result.Should().Be("Paid");
        }

        [Fact]
        public async Task GetPaymentStatusAsync_AsOtherUser_ThrowsUnauthorizedException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.GetPaymentStatusAsync(1, 2) // User 2 trying to access User 1's payment
            );
            exception.Message.Should().Contain("Not allowed to view this payment status");
        }

        [Fact]
        public async Task GetPaymentStatusAsync_WithNonExistentPayment_ReturnsNull()
        {
            // Act
            var result = await _service.GetPaymentStatusAsync(999, 1);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region UpdatePaymentStatusAsync Tests

        [Fact]
        public async Task UpdatePaymentStatusAsync_AsAdmin_UpdatesStatus()
        {
            // Act
            var result = await _service.UpdatePaymentStatusAsync(3, "Admin", 2, "Paid");

            // Assert
            result.Should().NotBeNull();
            result!.PaymentStatus.Should().Be("Paid");

            var payment = await _context.Payments.FindAsync(2);
            payment!.PaymentStatus.Should().Be("Paid");
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_AsUser_ThrowsUnauthorizedException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.UpdatePaymentStatusAsync(1, "User", 2, "Paid")
            );
            exception.Message.Should().Contain("Alleen een admin mag de betaalstatus wijzigen");
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_WithInvalidStatus_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdatePaymentStatusAsync(3, "Admin", 2, "InvalidStatus")
            );
            exception.Message.Should().Contain("Ongeldige status");
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_WithNonExistentPayment_ReturnsNull()
        {
            // Act
            var result = await _service.UpdatePaymentStatusAsync(3, "Admin", 999, "Paid");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_ToPaid_ArchivesPayment()
        {
            // Arrange
            _mockArchiveService
                .Setup(s => s.ArchiveAndDeletePaymentAsync(It.IsAny<Payments>(), 3))
                .ReturnsAsync((true, string.Empty));

            // Act
            var result = await _service.UpdatePaymentStatusAsync(3, "Admin", 2, "Paid");

            // Assert
            result.Should().NotBeNull();
            _mockArchiveService.Verify(
                s => s.ArchiveAndDeletePaymentAsync(It.IsAny<Payments>(), 3),
                Times.Once
            );
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Failed")]
        public async Task UpdatePaymentStatusAsync_ToNonPaidStatus_DoesNotArchive(string status)
        {
            // Act
            var result = await _service.UpdatePaymentStatusAsync(3, "Admin", 2, status);

            // Assert
            result.Should().NotBeNull();
            result!.PaymentStatus.Should().Be(status);
            _mockArchiveService.Verify(
                s => s.ArchiveAndDeletePaymentAsync(It.IsAny<Payments>(), It.IsAny<int>()),
                Times.Never
            );
        }

        #endregion

        #region DeletePaymentAsync Tests

        [Fact]
        public async Task DeletePaymentAsync_WithExistingPayment_ArchivesAndDeletes()
        {
            // Arrange
            _mockArchiveService
                .Setup(s => s.ArchiveAndDeletePaymentAsync(It.IsAny<Payments>(), 3))
                .ReturnsAsync((true, string.Empty));

            // Act
            var result = await _service.DeletePaymentAsync(2, 3);

            // Assert
            result.success.Should().BeTrue();
            result.ErrorMessage.Should().BeEmpty();
            _mockArchiveService.Verify(
                s => s.ArchiveAndDeletePaymentAsync(It.IsAny<Payments>(), 3),
                Times.Once
            );
        }

        [Fact]
        public async Task DeletePaymentAsync_WithNonExistentPayment_ReturnsFalse()
        {
            // Act
            var result = await _service.DeletePaymentAsync(999, 3);

            // Assert
            result.success.Should().BeFalse();
            result.ErrorMessage.Should().Be("Payment niet gevonden");
        }

        #endregion

        #region GetPaymentsByUserAsync Tests

        [Fact]
        public async Task GetPaymentsByUserAsync_ForOwnPayments_ReturnsPayments()
        {
            // Act
            var result = await _service.GetPaymentsByUserAsync(1, 1);

            // Assert
            result.Should().HaveCount(2);
            result.All(p => p.LicensePlate == "AB-123-CD").Should().BeTrue();
        }

        [Fact]
        public async Task GetPaymentsByUserAsync_AsAdminForOtherUser_ReturnsPayments()
        {
            // Act
            var result = await _service.GetPaymentsByUserAsync(1, 3); // Admin viewing User 1

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetPaymentsByUserAsync_AsUserForOtherUser_ThrowsUnauthorizedException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.GetPaymentsByUserAsync(1, 2) // User 2 trying to view User 1
            );
            exception.Message.Should().Contain("Not allowed to view payments of another user");
        }

        [Fact]
        public async Task GetPaymentsByUserAsync_OrdersByCreatedAtDescending()
        {
            // Act
            var result = await _service.GetPaymentsByUserAsync(1, 1);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(2); // Newer payment first
            result[1].Id.Should().Be(1);
        }

        #endregion

        #region RefundPaymentAsync Tests

        [Fact]
        public async Task RefundPaymentAsync_WithValidPayment_CreatesRefund()
        {
            // Act
            var result = await _service.RefundPaymentAsync(1, 3);

            // Assert
            result.Should().NotBeNull();
            result!.PaymentStatus.Should().Be("Refund");
            result.Cost.Should().Be(-2.50m); // Negative cost for refund

            // Check original payment is marked as Refunded
            var originalPayment = await _context.Payments.FindAsync(1);
            originalPayment!.PaymentStatus.Should().Be("Refunded");

            // Check refund entry exists
            var refundEntry = await _context.Payments
                .Where(p => p.PaymentStatus == "Refund")
                .FirstOrDefaultAsync();
            refundEntry.Should().NotBeNull();
            refundEntry!.Cost.Should().Be(-2.50m);
        }

        [Fact]
        public async Task RefundPaymentAsync_WithNonExistentPayment_ReturnsNull()
        {
            // Act
            var result = await _service.RefundPaymentAsync(999, 3);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task RefundPaymentAsync_WithAlreadyRefundedPayment_ReturnsNull()
        {
            // Arrange - First refund
            await _service.RefundPaymentAsync(1, 3);

            // Act - Try to refund again
            var result = await _service.RefundPaymentAsync(1, 3);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetPaymentHistoryAsync Tests

        [Fact]
        public async Task GetPaymentHistoryAsync_AsUser_ReturnsOwnPayments()
        {
            // Act
            var result = await _service.GetPaymentHistoryAsync(1, "User");

            // Assert
            result.Should().HaveCount(2);
            result.All(p => p.LicensePlate == "AB-123-CD").Should().BeTrue();
        }

        [Fact]
        public async Task GetPaymentHistoryAsync_AsAdmin_ReturnsAllPayments()
        {
            // Act
            var result = await _service.GetPaymentHistoryAsync(3, "Admin");

            // Assert
            result.Should().HaveCount(2); // All payments in database
        }

        [Fact]
        public async Task GetPaymentHistoryAsync_OrdersByCreatedAtDescending()
        {
            // Act
            var result = await _service.GetPaymentHistoryAsync(1, "User");

            // Assert
            result[0].Id.Should().Be(2); // Newer first
            result[1].Id.Should().Be(1);
        }

        #endregion

        #region CalculateUserTotalAsync Tests

        [Fact]
        public async Task CalculateUserTotalAsync_ReturnsCorrectTotal()
        {
            // Act
            var result = await _service.CalculateUserTotalAsync(1);

            // Assert
            var resultObj = result as dynamic;
            ((int)resultObj!.userId).Should().Be(1);
            ((int)resultObj.transactionCount).Should().Be(2);
            ((decimal)resultObj.total).Should().Be(7.50m); // 2.50 + 5.00
        }

        [Fact]
        public async Task CalculateUserTotalAsync_WithNoPayments_ReturnsZero()
        {
            // Act
            var result = await _service.CalculateUserTotalAsync(2); // User 2 has no payments

            // Assert
            var resultObj = result as dynamic;
            ((int)resultObj!.userId).Should().Be(2);
            ((int)resultObj.transactionCount).Should().Be(0);
            ((decimal)resultObj.total).Should().Be(0m);
        }

        [Fact]
        public async Task CalculateUserTotalAsync_RoundsToTwoDecimals()
        {
            // Arrange - Add payment with many decimals
            var payment = new Payments
            {
                UserId = 2,
                ParkingLotId = 1,
                LicensePlate = "XY-789-ZZ",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 1.123456789m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CalculateUserTotalAsync(2);

            // Assert
            var resultObj = result as dynamic;
            ((decimal)resultObj!.total).Should().Be(1.12m); // Rounded to 2 decimals
        }

        #endregion

        #region CalculateAdminTotalAsync Tests

        [Fact]
        public async Task CalculateAdminTotalAsync_WithUserId_ReturnsUserTotal()
        {
            // Act
            var result = await _service.CalculateAdminTotalAsync(1);

            // Assert
            var resultObj = result as dynamic;
            ((int)resultObj!.userId).Should().Be(1);
            ((int)resultObj.transactionCount).Should().Be(2);
            ((decimal)resultObj.total).Should().Be(7.50m);
        }

        [Fact]
        public async Task CalculateAdminTotalAsync_WithoutUserId_ReturnsAllTotal()
        {
            // Act
            var result = await _service.CalculateAdminTotalAsync(null);

            // Assert
            var resultObj = result as dynamic;
            ((int)resultObj!.userId).Should().Be(0);
            ((int)resultObj.transactionCount).Should().Be(2);
            ((decimal)resultObj.total).Should().Be(7.50m);
        }

        [Fact]
        public async Task CalculateAdminTotalAsync_WithNonExistentUser_ThrowsKeyNotFoundException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.CalculateAdminTotalAsync(999)
            );
            exception.Message.Should().Contain("User with ID 999 not found");
        }

        #endregion
    }
}