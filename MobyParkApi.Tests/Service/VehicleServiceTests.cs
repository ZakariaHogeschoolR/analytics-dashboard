using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;

namespace MobyParkApi.Tests.Service
{
    public class VehicleServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<VehiclesController>> _mockLogger;
        private readonly Mock<IArchiveService> _mockArchiveService;
        private readonly VehiclesService _service;

        public VehicleServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<VehiclesController>>();
            _mockArchiveService = new Mock<IArchiveService>();
            _service = new VehiclesService(_context, _mockLogger.Object, _mockArchiveService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SeedDatabase()
        {
            var user1 = new Users
            {
                Id = 1,
                Username = "user1",
                Role = "User",
                Name = "User One",
                Email = "user1@test.com",
                Phone_Number = "0612345678",
                Password = "hashedpassword",
                Birth_Year = 1990,
                Active = true,
                Created_At = DateTime.UtcNow
            };

            var user2 = new Users
            {
                Id = 2,
                Username = "user2",
                Role = "User",
                Name = "User Two",
                Email = "user2@test.com",
                Phone_Number = "0612345679",
                Password = "hashedpassword",
                Birth_Year = 1991,
                Active = true,
                Created_At = DateTime.UtcNow
            };

            _context.Users.AddRange(user1, user2);
            _context.SaveChanges();
        }

        private ClaimsPrincipal CreateClaimsPrincipal(int userId, string role = "User", string username = "testuser")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private Vehicles CreateTestVehicle(int userId, string licensePlate = "AB-123-C", int id = 0)
        {
            return new Vehicles
            {
                Id = id,
                LicensePlate = licensePlate,
                Make = "Toyota",
                Model = "Corolla",
                Color = "Blauw",
                Year = 2020,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
        }

        private CreateVehicleRequestDto CreateValidVehicleDto(string licensePlate = "AB-123-C")
        {
            return new CreateVehicleRequestDto
            {
                LicensePlate = licensePlate,
                Make = "Toyota",
                Model = "Corolla",
                Color = "Blauw",
                Year = 2020
            };
        }

        #endregion

        #region GetMyVehiclesService Tests

        [Fact]
        public async Task GetMyVehiclesService_ReturnsOnlyUserVehicles()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle1 = CreateTestVehicle(1, "AB-123-C", 1);
            var vehicle2 = CreateTestVehicle(1, "CD-456-E", 2);
            var vehicle3 = CreateTestVehicle(2, "EF-789-G", 3); // Different user
            
            _context.Vehicles.AddRange(vehicle1, vehicle2, vehicle3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMyVehiclesService(user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, v => Assert.Equal(1, v.UserId));
            Assert.Contains(result, v => v.LicensePlate == "AB-123-C");
            Assert.Contains(result, v => v.LicensePlate == "CD-456-E");
            Assert.DoesNotContain(result, v => v.LicensePlate == "EF-789-G");
        }

        [Fact]
        public async Task GetMyVehiclesService_NoVehicles_ReturnsEmptyList()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);

            // Act
            var result = await _service.GetMyVehiclesService(user);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMyVehiclesService_NoUserId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetMyVehiclesService(user));
        }

        [Fact]
        public async Task GetMyVehiclesService_OrdersByCreatedAtDescending()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle1 = CreateTestVehicle(1, "AB-123-C", 1);
            vehicle1.CreatedAt = DateTime.UtcNow.AddDays(-2);
            
            var vehicle2 = CreateTestVehicle(1, "CD-456-E", 2);
            vehicle2.CreatedAt = DateTime.UtcNow.AddDays(-1);
            
            _context.Vehicles.AddRange(vehicle1, vehicle2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMyVehiclesService(user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("CD-456-E", result[0].LicensePlate); // Most recent first
            Assert.Equal("AB-123-C", result[1].LicensePlate);
        }

        #endregion

        #region GetAllVehiclesService Tests

        [Fact]
        public async Task GetAllVehiclesService_ReturnsAllVehicles()
        {
            // Arrange
            SeedDatabase();
            
            var vehicle1 = CreateTestVehicle(1, "AB-123-C", 1);
            var vehicle2 = CreateTestVehicle(2, "CD-456-E", 2);
            
            _context.Vehicles.AddRange(vehicle1, vehicle2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllVehiclesService();

            // Assert
            Assert.NotNull(result);
            var vehiclesList = result.ToList();
            Assert.Equal(2, vehiclesList.Count);
        }

        [Fact]
        public async Task GetAllVehiclesService_IncludesUserName()
        {
            // Arrange
            SeedDatabase();
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllVehiclesService();

            // Assert
            Assert.NotNull(result);
            var vehicleList = result.ToList();
            Assert.Single(vehicleList);
            
            var vehicleObj = vehicleList[0];
            var userNameProperty = vehicleObj.GetType().GetProperty("userName");
            Assert.NotNull(userNameProperty);
            var userName = userNameProperty.GetValue(vehicleObj);
            Assert.Equal("user1", userName);
        }

        #endregion

        #region GetVehicleService Tests

        [Fact]
        public async Task GetVehicleService_ExistingVehicle_ReturnsVehicle()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetVehicleService(1, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("AB-123-C", result.LicensePlate);
            Assert.Equal(1, result.UserId);
        }

        [Fact]
        public async Task GetVehicleService_NonExistentVehicle_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetVehicleService(999, user));
            Assert.Contains("niet gevonden", exception.Message);
        }

        [Fact]
        public async Task GetVehicleService_OtherUserVehicle_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(2, "AB-123-C", 1); // Belongs to user 2
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetVehicleService(1, user));
        }

        [Fact]
        public async Task GetVehicleService_NoUserId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetVehicleService(1, user));
        }

        #endregion

        #region CreateVehicleService Tests

        [Fact]
        public async Task CreateVehicleService_ValidVehicle_CreatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto();

            // Act
            var result = await _service.CreateVehicleService(dto, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("AB-123-C", result.LicensePlate);
            Assert.Equal("Toyota", result.Make);
            Assert.Equal("Corolla", result.Model);
            Assert.Equal("Blauw", result.Color);
            Assert.Equal(2020, result.Year);
            Assert.Equal(1, result.UserId);
            Assert.NotNull(result.CreatedAt);

            var dbVehicle = await _context.Vehicles.FindAsync(result.Id);
            Assert.NotNull(dbVehicle);
        }

        [Fact]
        public async Task CreateVehicleService_UpperCaseLicensePlate_StoresUpperCase()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto("ab-123-c");

            // Act
            var result = await _service.CreateVehicleService(dto, user);

            // Assert
            Assert.Equal("AB-123-C", result.LicensePlate);
        }

        [Fact]
        public async Task CreateVehicleService_MissingLicensePlate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto();
            dto.LicensePlate = "";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateVehicleService(dto, user));
            Assert.Contains("Verplichte velden ontbreken", exception.Message);
        }

        [Fact]
        public async Task CreateVehicleService_InvalidLicensePlate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto("INVALID");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateVehicleService(dto, user));
            Assert.Contains("Ongeldig Nederlands kenteken format", exception.Message);
        }

        [Fact]
        public async Task CreateVehicleService_DuplicateLicensePlate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var existingVehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(existingVehicle);
            await _context.SaveChangesAsync();

            var dto = CreateValidVehicleDto("AB-123-C");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateVehicleService(dto, user));
            Assert.Contains("Kenteken bestaat al", exception.Message);
        }

        [Fact]
        public async Task CreateVehicleService_DuplicateLicensePlateDifferentUser_Allows()
        {
            // Arrange
            SeedDatabase();
            var user1 = CreateClaimsPrincipal(1);
            var user2 = CreateClaimsPrincipal(2);
            
            var existingVehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(existingVehicle);
            await _context.SaveChangesAsync();

            var dto = CreateValidVehicleDto("AB-123-C");

            // Act - User 2 can create vehicle with same license plate
            var result = await _service.CreateVehicleService(dto, user2);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.UserId);
            Assert.Equal("AB-123-C", result.LicensePlate);
        }

        [Fact]
        public async Task CreateVehicleService_MissingMake_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto();
            dto.Make = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateVehicleService(dto, user));
        }

        [Fact]
        public async Task CreateVehicleService_InvalidYear_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var dto = CreateValidVehicleDto();
            dto.Year = 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateVehicleService(dto, user));
        }

        [Fact]
        public async Task CreateVehicleService_NoUserId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            var dto = CreateValidVehicleDto();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateVehicleService(dto, user));
        }

        #endregion

        #region UpdateVehicleService Tests

        [Fact]
        public async Task UpdateVehicleService_ValidUpdate_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto
            {
                Make = "Honda",
                Model = "Civic",
                Color = "Rood",
                Year = 2021
            };

            // Act
            var result = await _service.UpdateVehicleService(1, updateDto, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Honda", result.Make);
            Assert.Equal("Civic", result.Model);
            Assert.Equal("Rood", result.Color);
            Assert.Equal(2021, result.Year);
            Assert.NotNull(result.ModifiedAt);
        }

        [Fact]
        public async Task UpdateVehicleService_UpdateLicensePlate_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto
            {
                LicensePlate = "CD-456-E"
            };

            // Act
            var result = await _service.UpdateVehicleService(1, updateDto, user);

            // Assert
            Assert.Equal("CD-456-E", result.LicensePlate);
        }

        [Fact]
        public async Task UpdateVehicleService_NonExistentVehicle_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            var updateDto = new UpdateVehicleRequestDto { Make = "Honda" };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateVehicleService(999, updateDto, user));
        }

        [Fact]
        public async Task UpdateVehicleService_OtherUserVehicle_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(2, "AB-123-C", 1); // Belongs to user 2
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto { Make = "Honda" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateVehicleService(1, updateDto, user));
        }

        [Fact]
        public async Task UpdateVehicleService_DuplicateLicensePlate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle1 = CreateTestVehicle(1, "AB-123-C", 1);
            var vehicle2 = CreateTestVehicle(1, "CD-456-E", 2);
            _context.Vehicles.AddRange(vehicle1, vehicle2);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto
            {
                LicensePlate = "CD-456-E" // Same as vehicle2
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateVehicleService(1, updateDto, user));
            Assert.Contains("al een ander voertuig", exception.Message);
        }

        [Fact]
        public async Task UpdateVehicleService_InvalidLicensePlate_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto
            {
                LicensePlate = "INVALID"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateVehicleService(1, updateDto, user));
            Assert.Contains("Ongeldig Nederlands kenteken format", exception.Message);
        }

        [Fact]
        public async Task UpdateVehicleService_PartialUpdate_UpdatesOnlyProvidedFields()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1);
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateVehicleRequestDto
            {
                Make = "Honda"
                // Only updating Make, other fields should remain unchanged
            };

            // Act
            var result = await _service.UpdateVehicleService(1, updateDto, user);

            // Assert
            Assert.Equal("Honda", result.Make);
            Assert.Equal("Corolla", result.Model); // Unchanged
            Assert.Equal("Blauw", result.Color); // Unchanged
            Assert.Equal(2020, result.Year); // Unchanged
        }

        #endregion

        #region DeleteVehicleService Tests

        [Fact]
        public async Task DeleteVehicleService_OwnVehicle_DeletesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1, "User");
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            _mockArchiveService
                .Setup(s => s.ArchiveVehicleAndReservationsAsync(It.IsAny<Vehicles>(), It.IsAny<string>()))
                .ReturnsAsync(0);

            // Act
            var result = await _service.DeleteVehicleService(1, user);

            // Assert
            Assert.Contains("succesvol verwijderd", result);
            _mockArchiveService.Verify(
                s => s.ArchiveVehicleAndReservationsAsync(It.IsAny<Vehicles>(), It.IsAny<string>()),
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteVehicleService_AdminCanDeleteAnyVehicle_DeletesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var admin = CreateClaimsPrincipal(99, "Admin");
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1); // Belongs to user 1
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            _mockArchiveService
                .Setup(s => s.ArchiveVehicleAndReservationsAsync(It.IsAny<Vehicles>(), It.IsAny<string>()))
                .ReturnsAsync(0);

            // Act
            var result = await _service.DeleteVehicleService(1, admin);

            // Assert
            Assert.Contains("succesvol verwijderd", result);
        }

        [Fact]
        public async Task DeleteVehicleService_NonExistentVehicle_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1, "User");

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteVehicleService(999, user));
        }

        [Fact]
        public async Task DeleteVehicleService_OtherUserVehicle_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1, "User");
            
            var vehicle = CreateTestVehicle(2, "AB-123-C", 1); // Belongs to user 2
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteVehicleService(1, user));
            Assert.Contains("eigen voertuigen verwijderen", exception.Message);
        }

        [Fact]
        public async Task DeleteVehicleService_NoUserId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteVehicleService(1, user));
        }

        [Fact]
        public async Task DeleteVehicleService_WithReservations_ArchivesReservations()
        {
            // Arrange
            SeedDatabase();
            var user = CreateClaimsPrincipal(1, "User");
            
            var vehicle = CreateTestVehicle(1, "AB-123-C", 1);
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            _mockArchiveService
                .Setup(s => s.ArchiveVehicleAndReservationsAsync(It.IsAny<Vehicles>(), It.IsAny<string>()))
                .ReturnsAsync(3); // 3 reservations archived

            // Act
            var result = await _service.DeleteVehicleService(1, user);

            // Assert
            Assert.Contains("3 reserveringen", result);
        }

        #endregion
    }
}
