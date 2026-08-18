using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;

namespace MobyParkApi.Tests.Service
{
    public class DiscountCodeServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<DiscountCodeService>> _mockLogger;
        private readonly DiscountCodeService _service;

        public DiscountCodeServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<DiscountCodeService>>();
            _service = new DiscountCodeService(_context, _mockLogger.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SeedDatabase()
        {
            var user = new Users
            {
                Id = 1,
                Username = "testuser",
                Role = "User",
                Name = "Test User",
                Email = "test@test.com",
                Phone_Number = "0612345678",
                Password = "hashedpassword",
                Birth_Year = 1990,
                Active = true,
                Created_At = DateTime.UtcNow
            };

            var parkingLot = new ParkingLots
            {
                Id = 1,
                Name = "Test Parking",
                Location = "Test Location",
                Address = "Test Address",
                Capacity = 10,
                Reserved = 0,
                Tariff = 5.0m,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.ParkingLots.Add(parkingLot);
            _context.SaveChanges();
        }

        private CreateDiscountCodeDto CreateValidDiscountCodeDto(
            string code = "TEST123",
            string discountType = "Percentage",
            decimal discountValue = 10.0m)
        {
            return new CreateDiscountCodeDto
            {
                Code = code,
                DiscountType = discountType,
                DiscountValue = discountValue,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                MaxUses = 100
            };
        }

        #endregion

        #region CreateDiscountCodeAsync Tests

        [Fact]
        public async Task CreateDiscountCode_HappyFlow_CreatesDiscountCodeSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();

            // Act
            var result = await _service.CreateDiscountCodeAsync(dto, 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TEST123", result.Code);
            Assert.Equal("Percentage", result.DiscountType);
            Assert.Equal(10.0m, result.DiscountValue);
            Assert.True(result.IsActive);
            Assert.Equal(0, result.CurrentUses);

            var dbCode = await _context.DiscountCodes.FirstOrDefaultAsync(dc => dc.Code == "TEST123");
            Assert.NotNull(dbCode);
            Assert.Equal("TEST123", dbCode.Code);
        }

        [Fact]
        public async Task CreateDiscountCode_DuplicateCode_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscountCodeAsync(dto, 1));
        }

        [Fact]
        public async Task CreateDiscountCode_InvalidPercentage_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto(discountValue: 150.0m); // > 100%

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscountCodeAsync(dto, 1));
            Assert.Contains("Percentage korting moet tussen 0 en 100 liggen", exception.Message);
        }

        [Fact]
        public async Task CreateDiscountCode_InvalidFixedAmount_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto(discountType: "FixedAmount", discountValue: -10.0m);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscountCodeAsync(dto, 1));
            Assert.Contains("Vast bedrag korting moet groter zijn dan 0", exception.Message);
        }

        [Fact]
        public async Task CreateDiscountCode_EndDateBeforeStartDate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.EndDate = dto.StartDate.AddDays(-1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscountCodeAsync(dto, 1));
            Assert.Contains("Einddatum moet na startdatum zijn", exception.Message);
        }

        [Fact]
        public async Task CreateDiscountCode_WithRestrictions_CreatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.AllowedParkingLotIds = new List<int> { 1 };
            dto.AllowedUserIds = new List<int> { 1 };
            dto.AllowedUserGroups = new List<string> { "User" };

            // Act
            var result = await _service.CreateDiscountCodeAsync(dto, 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.AllowedParkingLotIds);
            Assert.Contains(1, result.AllowedParkingLotIds);
            Assert.NotNull(result.AllowedUserIds);
            Assert.Contains(1, result.AllowedUserIds);
        }

        #endregion

        #region GetDiscountCodeByIdAsync Tests

        [Fact]
        public async Task GetDiscountCodeById_ExistingCode_ReturnsDiscountCode()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            var created = await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.GetDiscountCodeByIdAsync(created.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(created.Id, result.Id);
            Assert.Equal("TEST123", result.Code);
        }

        [Fact]
        public async Task GetDiscountCodeById_NonExistentCode_ReturnsNull()
        {
            // Arrange
            SeedDatabase();

            // Act
            var result = await _service.GetDiscountCodeByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetDiscountCodeByCodeAsync Tests

        [Fact]
        public async Task GetDiscountCodeByCode_ExistingCode_ReturnsDiscountCode()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.GetDiscountCodeByCodeAsync("TEST123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TEST123", result.Code);
        }

        [Fact]
        public async Task GetDiscountCodeByCode_CaseInsensitive_ReturnsDiscountCode()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.GetDiscountCodeByCodeAsync("test123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TEST123", result.Code);
        }

        [Fact]
        public async Task GetDiscountCodeByCode_NonExistentCode_ReturnsNull()
        {
            // Arrange
            SeedDatabase();

            // Act
            var result = await _service.GetDiscountCodeByCodeAsync("NONEXISTENT");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetAllDiscountCodesAsync Tests

        [Fact]
        public async Task GetAllDiscountCodes_ReturnsAllCodes()
        {
            // Arrange
            SeedDatabase();
            await _service.CreateDiscountCodeAsync(CreateValidDiscountCodeDto("CODE1"), 1);
            await _service.CreateDiscountCodeAsync(CreateValidDiscountCodeDto("CODE2"), 1);

            // Act
            var result = await _service.GetAllDiscountCodesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAllDiscountCodes_ActiveOnly_ReturnsOnlyActiveCodes()
        {
            // Arrange
            SeedDatabase();
            var activeDto = CreateValidDiscountCodeDto("ACTIVE");
            await _service.CreateDiscountCodeAsync(activeDto, 1);

            var inactiveDto = CreateValidDiscountCodeDto("INACTIVE");
            inactiveDto.IsActive = false;
            await _service.CreateDiscountCodeAsync(inactiveDto, 1);

            // Act
            var result = await _service.GetAllDiscountCodesAsync(activeOnly: true);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("ACTIVE", result.First().Code);
        }

        #endregion

        #region UpdateDiscountCodeAsync Tests

        [Fact]
        public async Task UpdateDiscountCode_ExistingCode_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var created = await _service.CreateDiscountCodeAsync(CreateValidDiscountCodeDto(), 1);
            var updateDto = new UpdateDiscountCodeDto
            {
                DiscountValue = 20.0m,
                IsActive = false
            };

            // Act
            var result = await _service.UpdateDiscountCodeAsync(created.Id, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(20.0m, result.DiscountValue);
            Assert.False(result.IsActive);
        }

        [Fact]
        public async Task UpdateDiscountCode_NonExistentCode_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var updateDto = new UpdateDiscountCodeDto { DiscountValue = 20.0m };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateDiscountCodeAsync(999, updateDto));
        }

        [Fact]
        public async Task UpdateDiscountCode_InvalidPercentage_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var created = await _service.CreateDiscountCodeAsync(CreateValidDiscountCodeDto(), 1);
            var updateDto = new UpdateDiscountCodeDto { DiscountValue = 150.0m };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateDiscountCodeAsync(created.Id, updateDto));
            Assert.Contains("Percentage korting moet tussen 0 en 100 liggen", exception.Message);
        }

        #endregion

        #region DeactivateDiscountCodeAsync Tests

        [Fact]
        public async Task DeactivateDiscountCode_ExistingCode_DeactivatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var created = await _service.CreateDiscountCodeAsync(CreateValidDiscountCodeDto(), 1);

            // Act
            var result = await _service.DeactivateDiscountCodeAsync(created.Id);

            // Assert
            Assert.True(result);
            var dbCode = await _context.DiscountCodes.FindAsync(created.Id);
            Assert.NotNull(dbCode);
            Assert.False(dbCode.IsActive);
        }

        [Fact]
        public async Task DeactivateDiscountCode_NonExistentCode_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeactivateDiscountCodeAsync(999));
        }

        #endregion

        #region ValidateDiscountCodeAsync Tests

        [Fact]
        public async Task ValidateDiscountCode_ValidCode_ReturnsValidResult()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            var created = await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Equal(10.0m, result.DiscountAmount); // 10% of 100
            Assert.Equal(90.0m, result.FinalCost);
        }

        [Fact]
        public async Task ValidateDiscountCode_NonExistentCode_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "NONEXISTENT",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("niet gevonden", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_InactiveCode_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.IsActive = false;
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("niet actief", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_ExpiredCode_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.StartDate = DateTime.UtcNow.AddDays(-30);
            dto.EndDate = DateTime.UtcNow.AddDays(-1); // Expired
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("verlopen", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_MaxUsesReached_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.MaxUses = 1;
            var created = await _service.CreateDiscountCodeAsync(dto, 1);
            
            // Use the code once
            await _service.ApplyDiscountCodeAsync("TEST123", 1, 1, DateTime.UtcNow.AddDays(1), 100.0m, null, null);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("maximale aantal gebruiken bereikt", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_UserNotAllowed_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.AllowedUserIds = new List<int> { 999 }; // Different user
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("niet geldig voor deze gebruiker", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_ParkingLotNotAllowed_ReturnsInvalid()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            dto.AllowedParkingLotIds = new List<int> { 999 }; // Different parking lot
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("niet geldig voor deze parkeerplaats", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_FixedAmount_CalculatesCorrectly()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto(discountType: "FixedAmount", discountValue: 15.0m);
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Equal(15.0m, result.DiscountAmount);
            Assert.Equal(85.0m, result.FinalCost);
        }

        [Fact]
        public async Task ValidateDiscountCode_FixedAmountExceedsCost_AppliesMaximumDiscount()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto(discountType: "FixedAmount", discountValue: 150.0m);
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var result = await _service.ValidateDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Equal(100.0m, result.DiscountAmount); // Should not exceed original cost
            Assert.Equal(0.0m, result.FinalCost);
        }

        #endregion

        #region ApplyDiscountCodeAsync Tests

        [Fact]
        public async Task ApplyDiscountCode_ValidCode_IncrementsUsage()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            await _service.CreateDiscountCodeAsync(dto, 1);

            // Act
            var discountAmount = await _service.ApplyDiscountCodeAsync(
                "TEST123",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m,
                reservationId: null,
                paymentId: null
            );

            // Assert
            Assert.Equal(10.0m, discountAmount);
            var dbCode = await _context.DiscountCodes.FirstOrDefaultAsync(dc => dc.Code == "TEST123");
            Assert.NotNull(dbCode);
            Assert.Equal(1, dbCode.CurrentUses);

            var usage = await _context.DiscountCodeUsage.FirstOrDefaultAsync(u => u.DiscountCodeId == dbCode.Id);
            Assert.NotNull(usage);
            Assert.Equal(10.0m, usage.DiscountAmount);
        }

        [Fact]
        public async Task ApplyDiscountCode_InvalidCode_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.ApplyDiscountCodeAsync(
                "INVALID",
                userId: 1,
                parkingLotId: 1,
                reservationStartTime: DateTime.UtcNow.AddDays(1),
                originalCost: 100.0m,
                reservationId: null,
                paymentId: null
            ));
        }

        #endregion

        #region GetDiscountCodeStatisticsAsync Tests

        [Fact]
        public async Task GetDiscountCodeStatistics_WithUsage_ReturnsStatistics()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidDiscountCodeDto();
            var created = await _service.CreateDiscountCodeAsync(dto, 1);
            
            // Apply code twice
            await _service.ApplyDiscountCodeAsync("TEST123", 1, 1, DateTime.UtcNow.AddDays(1), 100.0m, null, null);
            await _service.ApplyDiscountCodeAsync("TEST123", 1, 1, DateTime.UtcNow.AddDays(1), 200.0m, null, null);

            // Act
            var statistics = await _service.GetDiscountCodeStatisticsAsync(created.Id);

            // Assert
            Assert.NotNull(statistics);
            Assert.Equal(2, statistics.TotalUses);
            Assert.Equal(30.0m, statistics.TotalDiscountAmount); // 10% of 100 + 10% of 200
            Assert.Equal(300.0m, statistics.TotalOriginalAmount);
        }

        [Fact]
        public async Task GetDiscountCodeStatistics_NonExistentCode_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetDiscountCodeStatisticsAsync(999));
        }

        #endregion
    }
}
