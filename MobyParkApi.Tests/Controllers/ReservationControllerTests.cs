using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;

namespace MobyParkApi.Tests.Controllers
{
    public class ReservationControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IReservationService> _mockReservationService;
        private readonly Mock<ILogger<ReservationController>> _mockLogger;
        private readonly ReservationController _controller;
        private readonly Mock<IReservationAutoCompleteService> _reservationAutoCompleteServiceMock;

        public ReservationControllerTests()
        {
            // Setup InMemory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            
            // Setup mocks
            _mockReservationService = new Mock<IReservationService>();
            _mockLogger = new Mock<ILogger<ReservationController>>();

            var mockAutoCompleteService = new Mock<IReservationAutoCompleteService>();
            
            // Create controller with mocked dependencies
            _controller = new ReservationController(
                _mockReservationService.Object,
                _context,
                _mockLogger.Object,
                mockAutoCompleteService.Object
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SetupUser(int userId, string username, string role)
        {
            // Setup claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Add user to database
            var user = new Users
            {
                Id = userId,
                Username = username,
                Role = role,
                Name = username,
                Email = $"{username}@test.com",
                Phone_Number = "0612345678",
                Password = "hashedpassword",
                Birth_Year = 1990,
                Active = true,
                Created_At = DateTime.UtcNow,
                Modified_At = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.SaveChanges();
        }

        private ReservationDto CreateReservationDto(
            string licensePlate = "AB-123-CD",
            string startDate = null,
            string endDate = null,
            int parkingLotId = 1)
        {
            startDate ??= DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
            endDate ??= DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss");

            return new ReservationDto
            {
                LicensePlate = licensePlate,
                StartDate = startDate,
                EndDate = endDate,
                ParkingLotId = parkingLotId
            };
        }

        private ReservationResponseDto CreateReservationResponseDto(
            int id = 1,
            int parkingLotId = 1,
            int vehicleId = 1,
            decimal cost = 1.9m)
        {
            return new ReservationResponseDto
            {
                Id = id,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = cost,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null,
                ParkingLot = new ParkingLotSummaryDto
                {
                    Id = parkingLotId,
                    Name = "Test Parkeerplaats",
                    Location = "Test Location",
                    Address = "Test Straat 1",
                    Tariff = 1.9m,
                    DayTariff = 10m,
                    Capacity = 100
                },
                Vehicle = new VehicleSummaryDto
                {
                    Id = vehicleId,
                    LicensePlate = "AB-123-CD",
                    Make = "Mercedes",
                    Model = "GLA",
                    Color = "Red",
                    Year = 2025
                }
            };
        }

        #endregion

        #region POST /api/reservation Tests

        [Fact]
        public async Task PostReservation_HappyFlow_ReturnsCreatedWithCorrectCost()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto();
            var expectedResponse = CreateReservationResponseDto(cost: 1.9m);

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var reservation = Assert.IsType<ReservationResponseDto>(createdResult.Value);
            
            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(expectedResponse.Id, reservation.Id);
            Assert.Equal(expectedResponse.Cost, reservation.Cost);
            Assert.Equal("Pending", reservation.Status);
            
            _mockReservationService.Verify(
                s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), 1), 
                Times.Once
            );
        }

        [Fact]
        public async Task PostReservation_KentekenNietVanGebruiker_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto(licensePlate: "XX-999-YY");

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ThrowsAsync(new ArgumentException("Kenteken niet gevonden of niet van jou"));

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Kenteken niet gevonden of niet van jou", errorProperty?.ToString());
        }

        [Fact]
        public async Task PostReservation_GeenBeschikbaarheidInPeriode_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto();

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ThrowsAsync(new ArgumentException("Geen beschikbare plekken in deze periode"));

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Geen beschikbare plekken in deze periode", errorProperty?.ToString());
        }

        [Fact]
        public async Task PostReservation_StarttijdInVerleden_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto(
                startDate: DateTime.UtcNow.AddHours(-2).ToString("yyyy-MM-dd HH:mm:ss"),
                endDate: DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss")
            );

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ThrowsAsync(new ArgumentException("Starttijd moet in de toekomst zijn"));

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Starttijd moet in de toekomst zijn", errorProperty?.ToString());
        }

        [Fact]
        public async Task PostReservation_EindtijdVoorStarttijd_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto(
                startDate: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss"),
                endDate: DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss")
            );

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ThrowsAsync(new ArgumentException("Eindtijd moet na starttijd zijn"));

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Eindtijd moet na starttijd zijn", errorProperty?.ToString());
        }

        [Fact]
        public async Task PostReservation_ParkeerplaatsBestaatNiet_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto(parkingLotId: 9999);

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException("Parkeerplaats niet gevonden"));

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Parkeerplaats niet gevonden", errorProperty?.ToString());
        }

        [Fact]
        public async Task PostReservation_KostenBerekeningCorrect_Returns1Hour1Point9Euro()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var startTime = DateTime.UtcNow.AddHours(1);
            var endTime = startTime.AddHours(1); // 1 uur
            
            var dto = CreateReservationDto(
                startDate: startTime.ToString("yyyy-MM-dd HH:mm:ss"),
                endDate: endTime.ToString("yyyy-MM-dd HH:mm:ss")
            );
            
            var expectedResponse = CreateReservationResponseDto(cost: 1.9m); // 1 uur * €1.90 tarief

            _mockReservationService
                .Setup(s => s.CreateReservationAsync(It.IsAny<ReservationDto>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var reservation = Assert.IsType<ReservationResponseDto>(createdResult.Value);
            
            Assert.Equal(1.9m, reservation.Cost);
        }

        [Fact]
        public async Task PostReservation_GebruikerNietIngevuld_ReturnsUnauthorized()
        {
            // Arrange - geen user setup, controller heeft geen authenticated user
            var dto = CreateReservationDto();

            // Act
            var result = await _controller.CreateReservation(dto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = unauthorizedResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(401, unauthorizedResult.StatusCode);
            Assert.Equal("Gebruiker niet gevonden", errorProperty?.ToString());
        }

        #endregion

        #region GET /api/reservation Tests

        [Fact]
        public async Task GetAllReservations_EigenReserveringen_ReturnsOkWithReservations()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var expectedReservations = new List<ReservationResponseDto>
            {
                CreateReservationResponseDto(id: 1),
                CreateReservationResponseDto(id: 2)
            };

            _mockReservationService
                .Setup(s => s.GetAllUserReservationsAsync(1))
                .ReturnsAsync(expectedReservations);

            // Act
            var result = await _controller.GetAllReservations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var reservations = Assert.IsType<List<ReservationResponseDto>>(okResult.Value);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(2, reservations.Count);
            
            _mockReservationService.Verify(
                s => s.GetAllUserReservationsAsync(1), 
                Times.Once
            );
        }

        [Fact]
        public async Task GetAllReservations_ReserveringenVanAnderenNietZichtbaar_ReturnsOnlyOwnReservations()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            
            // Service retourneert alleen reserveringen van user 1
            var expectedReservations = new List<ReservationResponseDto>
            {
                CreateReservationResponseDto(id: 1)
            };

            _mockReservationService
                .Setup(s => s.GetAllUserReservationsAsync(1))
                .ReturnsAsync(expectedReservations);

            // Act
            var result = await _controller.GetAllReservations();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var reservations = Assert.IsType<List<ReservationResponseDto>>(okResult.Value);
            
            // Alleen 1 reservering (eigen reservering)
            Assert.Single(reservations);
            
            // Verify dat alleen eigen userId werd gebruikt
            _mockReservationService.Verify(
                s => s.GetAllUserReservationsAsync(1), 
                Times.Once
            );
        }

        #endregion

        #region GET /api/reservation/{id} Tests

        [Fact]
        public async Task GetReservationById_EigenReservering_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var expectedReservation = CreateReservationResponseDto(id: 1);

            _mockReservationService
                .Setup(s => s.GetReservationByIdAsync(1, 1, "User"))
                .ReturnsAsync(expectedReservation);

            // Act
            var result = await _controller.GetReservationById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var reservation = Assert.IsType<ReservationResponseDto>(okResult.Value);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(1, reservation.Id);
            
            _mockReservationService.Verify(
                s => s.GetReservationByIdAsync(1, 1, "User"), 
                Times.Once
            );
        }

        [Fact]
        public async Task GetReservationById_ReserveringVanAndereGebruiker_ReturnsForbidden()
        {
            // Arrange
            SetupUser(1, "testuser", "User");

            _mockReservationService
                .Setup(s => s.GetReservationByIdAsync(2, 1, "User"))
                .ThrowsAsync(new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering"));

            // Act
            var result = await _controller.GetReservationById(2);

            // Assert
            Assert.IsType<ForbidResult>(result);
            
            _mockReservationService.Verify(
                s => s.GetReservationByIdAsync(2, 1, "User"), 
                Times.Once
            );
        }

        [Fact]
        public async Task GetReservationById_AdminKanAlleReserveringenZien_ReturnsOk()
        {
            // Arrange
            SetupUser(99, "admin", "Admin");
            var expectedReservation = CreateReservationResponseDto(id: 1);

            _mockReservationService
                .Setup(s => s.GetReservationByIdAsync(1, 99, "Admin"))
                .ReturnsAsync(expectedReservation);

            // Act
            var result = await _controller.GetReservationById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var reservation = Assert.IsType<ReservationResponseDto>(okResult.Value);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(1, reservation.Id);
        }

        [Fact]
        public async Task GetReservationById_ReserveringBestaatNiet_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "testuser", "User");

            _mockReservationService
                .Setup(s => s.GetReservationByIdAsync(9999, 1, "User"))
                .ReturnsAsync((ReservationResponseDto)null);

            // Act
            var result = await _controller.GetReservationById(9999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Reservering niet gevonden", errorProperty?.ToString());
        }

        #endregion

        #region DELETE /api/reservation/{id} Tests

        [Fact]
        public async Task DeleteReservation_SuccesvolVerwijderenEigenReservering_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "testuser", "User");

            _mockReservationService
                .Setup(s => s.DeleteReservationAsync(1, 1, "User", "testuser"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteReservation(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            var messageProperty = response?.GetType().GetProperty("message")?.GetValue(response, null);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Contains("geannuleerd", messageProperty?.ToString());
            
            _mockReservationService.Verify(
                s => s.DeleteReservationAsync(1, 1, "User", "testuser"), 
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteReservation_ReserveringVanAndereGebruiker_ReturnsForbidden()
        {
            // Arrange
            SetupUser(1, "testuser", "User");

            _mockReservationService
                .Setup(s => s.DeleteReservationAsync(2, 1, "User", "testuser"))
                .ThrowsAsync(new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering"));

            // Act
            var result = await _controller.DeleteReservation(2);

            // Assert
            Assert.IsType<ForbidResult>(result);
            
            _mockReservationService.Verify(
                s => s.DeleteReservationAsync(2, 1, "User", "testuser"), 
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteReservation_AdminKanAlleReserveringenVerwijderen_ReturnsOk()
        {
            // Arrange
            SetupUser(99, "admin", "Admin");

            _mockReservationService
                .Setup(s => s.DeleteReservationAsync(1, 99, "Admin", "admin"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteReservation(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            var messageProperty = response?.GetType().GetProperty("message")?.GetValue(response, null);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Contains("geannuleerd", messageProperty?.ToString());
        }

        [Fact]
        public async Task DeleteReservation_ReserveringBestaatNiet_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "testuser", "User");

            _mockReservationService
                .Setup(s => s.DeleteReservationAsync(9999, 1, "User", "testuser"))
                .ThrowsAsync(new KeyNotFoundException("Reservering niet gevonden"));

            // Act
            var result = await _controller.DeleteReservation(9999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Reservering niet gevonden", errorProperty?.ToString());
        }

        #endregion

        #region PUT /api/reservation/{id} Tests

        [Fact]
        public async Task UpdateReservation_SuccesvolUpdatenEigenReservering_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto();
            var expectedResponse = CreateReservationResponseDto(id: 1);

            _mockReservationService
                .Setup(s => s.UpdateReservationAsync(1, It.IsAny<ReservationDto>(), 1, "User"))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateReservation(1, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var reservation = Assert.IsType<ReservationResponseDto>(okResult.Value);
            
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(1, reservation.Id);
            
            _mockReservationService.Verify(
                s => s.UpdateReservationAsync(1, It.IsAny<ReservationDto>(), 1, "User"), 
                Times.Once
            );
        }

        [Fact]
        public async Task UpdateReservation_ReserveringVanAndereGebruiker_ReturnsForbidden()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto();

            _mockReservationService
                .Setup(s => s.UpdateReservationAsync(2, It.IsAny<ReservationDto>(), 1, "User"))
                .ThrowsAsync(new UnauthorizedAccessException("Je hebt geen toegang tot deze reservering"));

            // Act
            var result = await _controller.UpdateReservation(2, dto);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UpdateReservation_GeenBeschikbaarheidInNieuwePeriode_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "testuser", "User");
            var dto = CreateReservationDto();

            _mockReservationService
                .Setup(s => s.UpdateReservationAsync(1, It.IsAny<ReservationDto>(), 1, "User"))
                .ThrowsAsync(new ArgumentException("Geen beschikbare plekken in deze periode"));

            // Act
            var result = await _controller.UpdateReservation(1, dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);
            
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Geen beschikbare plekken in deze periode", errorProperty?.ToString());
        }

        #endregion
    }
}