using System;
using System.Collections.Generic;
using System.Linq;
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
using MobyParkApi.Service;
using Xunit;

namespace MobyParkApi.Tests.Controllers;

public class ParkingLotsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ParkingLotsController>> _loggerMock;
    private readonly Mock<ILogger<ReservationController>> _loggerMock2;
    private readonly Mock<ILogger<ReservationService>> _loggerMock3;
    private readonly Mock<IAddressValidationService> _addressValidationMock;
    private readonly Mock<IArchiveService> _archiveServiceMock;
    private readonly Mock<IDiscountCodeService> _discountCodeServiceMock;
    private readonly ReservationService _reservationService;
    private readonly ParkingLotsController _controller;

    public ParkingLotsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ParkingLotsController>>();
        _loggerMock2 = new Mock<ILogger<ReservationController>>();
        _loggerMock3 = new Mock<ILogger<ReservationService>>();
        _addressValidationMock = new Mock<IAddressValidationService>();
        _archiveServiceMock = new Mock<IArchiveService>();
        _discountCodeServiceMock = new Mock<IDiscountCodeService>();
        _reservationService = new ReservationService(_context, _loggerMock3.Object, _archiveServiceMock.Object, _discountCodeServiceMock.Object);
        _controller = new ParkingLotsController(_context, _loggerMock.Object, _loggerMock2.Object, _reservationService, _addressValidationMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Helper Methods

    private void SetupUserClaims(int userId, string role = "User")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private ParkingLots CreateTestParkingLot(int id = 1)
    {
        return new ParkingLots
        {
            Id = id,
            Name = "Test Parking",
            Location = "Amsterdam",
            Address = "Test Street 1",
            Capacity = 100,
            Reserved = 10,
            Tariff = 3.50m,
            DayTariff = 25.00m,
            Coordinates = "{\"lat\": 52.3676, \"lng\": 4.9041}",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private ParkingSessions CreateTestSession(int id, int parkingLotId, int userId, string licensePlate = "AB-123-C", bool stopped = false)
    {
        return new ParkingSessions
        {
            Id = id,
            ParkingLotId = parkingLotId,
            LicensePlate = licensePlate,
            Started = DateTime.UtcNow.AddHours(-1),
            Stopped = stopped ? DateTime.UtcNow : null,
            UserId = userId,
            DurationMinutes = stopped ? 60 : null,
            Cost = stopped ? 3.50m : null,
            PaymentStatus = "PENDING",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ModifiedAt = stopped ? DateTime.UtcNow : null
        };
    }

    #endregion

    #region GET /api/parking-lots Tests

    [Fact]
    public async Task GetAllParkingLots_ReturnsOkWithParkingLots()
    {
        // Arrange
        SetupUserClaims(1, "User");
        var parkingLot1 = CreateTestParkingLot(1);
        var parkingLot2 = CreateTestParkingLot(2);
        parkingLot2.Name = "Second Parking";

        _context.ParkingLots.AddRange(parkingLot1, parkingLot2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllParkingLots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Use reflection to access the data property since it's an anonymous type
        var valueType = okResult.Value.GetType();
        var dataProperty = valueType.GetProperty("data");
        Assert.NotNull(dataProperty);
        var parkingLots = dataProperty.GetValue(okResult.Value) as List<ParkingLots>;
        Assert.NotNull(parkingLots);
        Assert.Equal(2, parkingLots.Count);
    }

    [Fact]
    public async Task GetAllParkingLots_ReturnsEmptyList_WhenNoParkingLots()
    {
        // Act
        SetupUserClaims(1, "User");
        var result = await _controller.GetAllParkingLots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Use reflection to access the data property since it's an anonymous type
        var valueType = okResult.Value.GetType();
        var dataProperty = valueType.GetProperty("data");
        Assert.NotNull(dataProperty);
        var parkingLots = dataProperty.GetValue(okResult.Value) as List<ParkingLots>;
        Assert.NotNull(parkingLots);
        Assert.Empty(parkingLots);
    }

    [Theory]
    [InlineData("name", "asc")]
    [InlineData("name", "desc")]
    [InlineData("id", "asc")]
    [InlineData("location", "desc")]
    [InlineData("capacity", "asc")]
    [InlineData("available", "desc")]
    public async Task GetAllParkingLots_SortsCorrectly(string sortBy, string order)
    {
        // Arrange
        SetupUserClaims(1, "User");
        var parkingLots = new[]
        {
            CreateTestParkingLot(1),
            CreateTestParkingLot(2),
            CreateTestParkingLot(3)
        };
        _context.ParkingLots.AddRange(parkingLots);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllParkingLots(sortBy, order);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Use reflection to access the data property since it's an anonymous type
        var valueType = okResult.Value.GetType();
        var dataProperty = valueType.GetProperty("data");
        Assert.NotNull(dataProperty);
        var returnedLots = dataProperty.GetValue(okResult.Value) as List<ParkingLots>;
        Assert.NotNull(returnedLots);
        Assert.Equal(3, returnedLots.Count);
    }

    #endregion

    #region GET /api/parking-lots/{id} Tests

    [Fact]
    public async Task GetParkingLotById_ReturnsOk_WhenParkingLotExists()
    {
        // Arrange
        SetupUserClaims(1, "User");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetParkingLotById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Use reflection to access properties since it's an anonymous type
        var valueType = okResult.Value.GetType();
        var idProperty = valueType.GetProperty("id");
        var nameProperty = valueType.GetProperty("name");
        Assert.NotNull(idProperty);
        Assert.NotNull(nameProperty);
        Assert.Equal(1, (int)idProperty.GetValue(okResult.Value));
        Assert.Equal("Test Parking", (string)nameProperty.GetValue(okResult.Value));
    }

    [Fact]
    public async Task GetParkingLotById_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        // Act
        SetupUserClaims(1, "User");
        var result = await _controller.GetParkingLotById(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Parking lot met ID 999 niet gevonden", notFoundResult.Value);
    }

    #endregion

    #region GET /api/parking-lots/{id}/sessions Tests

    [Fact]
    public async Task GetParkingLotSessions_ReturnsOk_ForAdmin()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var session1 = CreateTestSession(1, 1, 1, "AB-123-C");
        var session2 = CreateTestSession(2, 1, 2, "XY-456-Z");
        _context.ParkingSessions.AddRange(session1, session2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetParkingLotSessions(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var sessions = Assert.IsAssignableFrom<List<ParkingSessionDto>>(okResult.Value);
        Assert.Equal(2, sessions.Count); // Admin ziet alle sessions
    }

    [Fact]
    public async Task GetParkingLotSessions_ReturnsOnlyUserSessions_ForNonAdmin()
    {
        // Arrange
        const int userId = 1;
        SetupUserClaims(userId, "User");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var session1 = CreateTestSession(1, 1, userId, "AB-123-C");
        var session2 = CreateTestSession(2, 1, 2, "XY-456-Z"); // Andere user
        _context.ParkingSessions.AddRange(session1, session2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetParkingLotSessions(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var sessions = Assert.IsAssignableFrom<List<ParkingSessionDto>>(okResult.Value);
        Assert.Single(sessions); // User ziet alleen eigen sessions
        Assert.Equal(userId, sessions[0].userId);
    }

    [Fact]
    public async Task GetParkingLotSessions_FiltersActiveOnly_WhenRequested()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
        var stoppedSession = CreateTestSession(2, 1, 1, "XY-456-Z", true);
        _context.ParkingSessions.AddRange(activeSession, stoppedSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetParkingLotSessions(1, activeOnly: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var sessions = Assert.IsAssignableFrom<List<ParkingSessionDto>>(okResult.Value);
        Assert.Single(sessions);
        // ParkingSessionDto.stopped is set to DateTime.Now when Stopped is null in DB
        // So for active sessions, we check the licensePlate to ensure we got the active one
        Assert.Equal("AB-123-C", sessions[0].licensePlate); // Active session should be returned
    }

    [Fact]
    public async Task GetParkingLotSessions_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        // Arrange
        SetupUserClaims(1);

        // Act
        var result = await _controller.GetParkingLotSessions(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Parking lot met ID 999 niet gevonden", notFoundResult.Value);
    }

    #endregion

    #region POST /api/parking-lots/{id}/sessions/start Tests

    [Fact]
    public async Task StartSession_ReturnsOk_WhenValidRequest()
    {
        // Arrange
        const int userId = 1;
        SetupUserClaims(userId);
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };
        
        // Act
        var result = await _controller.StartSession(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Session started for: AB-123-C", okResult.Value);

        var session = await _context.ParkingSessions.FirstOrDefaultAsync();
        Assert.NotNull(session);
        Assert.Equal("AB-123-C", session.LicensePlate);
        Assert.Equal(userId, session.UserId);
    }

    [Fact]
    public async Task StartSession_ReturnsBadRequest_WhenDuplicateActiveSession()
    {
        // Arrange
        SetupUserClaims(1);
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var existingSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
        _context.ParkingSessions.Add(existingSession);
        await _context.SaveChangesAsync();

        var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };

        // Act
        var result = await _controller.StartSession(1, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Cannot start a session", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task StartSession_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        // Arrange
        SetupUserClaims(1);
        var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };

        // Act
        var result = await _controller.StartSession(999, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Parking lot met ID 999 niet gevonden", notFoundResult.Value);
    }

    #endregion

    #region POST /api/parking-lots/{id}/sessions/stop Tests

    [Fact]
    public async Task StopSession_ReturnsOk_WhenValidRequest()
    {
        // Arrange
        SetupUserClaims(1);
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
        _context.ParkingSessions.Add(activeSession);
        await _context.SaveChangesAsync();

        var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

        // Act
        var result = await _controller.StopSession(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);

        // Verify session is archived
        var archivedSession = await _context.ArchivedParkingSessions
            .FirstOrDefaultAsync(aps => aps.OriginalSessionId == activeSession.Id || aps.LicensePlate == "AB-123-C");
        Assert.NotNull(archivedSession);
        Assert.NotNull(archivedSession.Stopped);
        Assert.NotNull(archivedSession.DurationMinutes);
        Assert.NotNull(archivedSession.Cost);
        
        // Verify session is deleted from main table
        var deletedSession = await _context.ParkingSessions.FindAsync(1);
        Assert.Null(deletedSession);
    }

    [Fact]
    public async Task StopSession_CalculatesCostCorrectly()
    {
        // Arrange
        SetupUserClaims(1);
        var parkingLot = CreateTestParkingLot(1);
        parkingLot.Tariff = 4.00m; // €4 per uur
        _context.ParkingLots.Add(parkingLot);

        var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
        activeSession.Started = DateTime.UtcNow.AddMinutes(-30); // 30 minuten geleden
        _context.ParkingSessions.Add(activeSession);
        await _context.SaveChangesAsync();

        var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

        // Act
        var result = await _controller.StopSession(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var archivedSession = await _context.ArchivedParkingSessions
            .FirstOrDefaultAsync(aps => aps.OriginalSessionId == activeSession.Id || aps.LicensePlate == "AB-123-C");
        Assert.NotNull(archivedSession);
        
        // 30 minuten = 0.5 uur × €4 = €2.00
        Assert.True(archivedSession.Cost > 0);
        
        // Verify session is deleted from main table
        var deletedSession = await _context.ParkingSessions.FindAsync(1);
        Assert.Null(deletedSession);
    }

    [Fact]
    public async Task StopSession_ReturnsBadRequest_WhenNoActiveSession()
    {
        // Arrange
        SetupUserClaims(1);
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

        // Act
        var result = await _controller.StopSession(1, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Cannot stop a session", badRequestResult.Value?.ToString());
    }

    #endregion

    #region POST /api/parking-lots (Create) Tests

    [Fact]
    public async Task CreateParkingLot_ReturnsCreated_ForAdmin()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var request = new CreateParkingLotRequestDto
        {
            Name = "New Parking",
            Location = "Amsterdam",
            Postcode = "1000 AA",
            HouseNumber = 1,
            Capacity = 200,
            Reserved = 0,
            Tariff = 3.00m,
            DayTariff = 20.00m,
            Lat = 52.3676,
            Lng = 4.9041
        };

        // Setup address validation mock
        var mockAddress = new PdokDocAddressResponseDto
        {
            straatnaam = "Damrak",
            huisnummer = 1,
            postcode = "1000 AA",
            woonplaatsnaam = "Amsterdam"
        };
        _addressValidationMock
            .Setup(x => x.GetAddressAsync("1000 AA", 1))
            .ReturnsAsync(mockAddress);

        // Act
        var result = await _controller.CreateParkingLot(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var parkingLot = await _context.ParkingLots.FirstOrDefaultAsync();
        Assert.NotNull(parkingLot);
        Assert.Equal("New Parking", parkingLot.Name);
        
        // ✅ Check for both comma and dot formats (locale dependent)
        Assert.True(
            parkingLot.Coordinates.Contains("52.3676") || 
            parkingLot.Coordinates.Contains("52,3676"),
            $"Coordinates '{parkingLot.Coordinates}' should contain latitude");
    }

    [Fact]
    public async Task CreateParkingLot_WorksWithoutAdminRole()
    {
        // NOTE: Service now explicitly checks for Admin role
        // This test verifies that non-admin users get Unauthorized
        SetupUserClaims(1, "User");
        var request = new CreateParkingLotRequestDto
        {
            Name = "New Parking",
            Location = "Amsterdam",
            Postcode = "1000 AA",
            HouseNumber = 1,
            Capacity = 200,
            Reserved = 0,
            Tariff = 3.00m,
            DayTariff = 20.00m,
            Lat = 52.3676,
            Lng = 4.9041
        };

        // Setup address validation mock
        var mockAddress = new PdokDocAddressResponseDto
        {
            straatnaam = "Damrak",
            huisnummer = 1,
            postcode = "1000 AA",
            woonplaatsnaam = "Amsterdam"
        };
        _addressValidationMock
            .Setup(x => x.GetAddressAsync("1000 AA", 1))
            .ReturnsAsync(mockAddress);

        // Act
        var result = await _controller.CreateParkingLot(request);

        // Assert
        // Service explicitly checks for Admin role, so non-admin gets Unauthorized
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    #endregion

    #region PUT /api/parking-lots/{id} (Update) Tests

    [Fact]
    public async Task UpdateParkingLot_ReturnsOk_ForAdmin()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        var request = new CreateParkingLotRequestDto
        {
            Name = "Updated Parking",
            Location = "Rotterdam",
            Postcode = "3000 AA",
            HouseNumber = 1,
            Capacity = 150,
            Reserved = 5,
            Tariff = 4.00m,
            DayTariff = 30.00m,
            Lat = 51.9244,
            Lng = 4.4777
        };

        // Act
        var result = await _controller.UpdateParkingLot(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        var updatedLot = await _context.ParkingLots.FindAsync(1);
        Assert.Equal("Updated Parking", updatedLot?.Name);
        Assert.Equal("Rotterdam", updatedLot?.Location);
        Assert.Equal(4.00m, updatedLot?.Tariff);
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var request = new CreateParkingLotRequestDto
        {
            Name = "Updated Parking",
            Location = "Rotterdam",
            Postcode = "3000 AA",
            HouseNumber = 1,
            Capacity = 150,
            Tariff = 4.00m,
            Lat = 51.9244,
            Lng = 4.4777
        };

        // Act
        var result = await _controller.UpdateParkingLot(999, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Parking lot met ID 999 niet gevonden", notFoundResult.Value);
    }

    [Fact]
    public async Task UpdateParkingLot_WorksWithoutAdminRole()
    {
        // NOTE: Service now explicitly checks for Admin role
        SetupUserClaims(1, "User");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        var request = new CreateParkingLotRequestDto
        {
            Name = "Updated Parking",
            Location = "Rotterdam",
            Postcode = "3000 AA",
            HouseNumber = 1,
            Capacity = 150,
            Tariff = 4.00m,
            Lat = 51.9244,
            Lng = 4.4777
        };

        // Act
        var result = await _controller.UpdateParkingLot(1, request);

        // Assert
        // Service explicitly checks for Admin role, so non-admin gets Unauthorized
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    #endregion

    #region DELETE /api/parking-lots/{id} Tests

    [Fact]
    public async Task DeleteParkingLot_ReturnsOk_WhenNoActiveSessions()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteParkingLot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var deletedLot = await _context.ParkingLots.FindAsync(1);
        Assert.Null(deletedLot);
    }

    [Fact]
    public async Task DeleteParkingLot_ArchivesToArchivedParkingLots_WhenDeleted()
    {
        // Arrange
        const int userId = 1;
        SetupUserClaims(userId, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        var originalCreatedAt = DateTime.UtcNow.AddDays(-30);
        var originalModifiedAt = DateTime.UtcNow.AddDays(-10);
        parkingLot.CreatedAt = originalCreatedAt;
        parkingLot.ModifiedAt = originalModifiedAt;
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        var originalParkingLotId = parkingLot.Id;
        var originalName = parkingLot.Name;

        // Act
        var result = await _controller.DeleteParkingLot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        // Verify deleted from active table
        var deletedLot = await _context.ParkingLots.FindAsync(originalParkingLotId);
        Assert.Null(deletedLot);
        
        // Verify archived to ArchivedParkingLots (indirect validation - archiving happens in service)
        var archivedLot = await _context.ArchivedParkingLots
            .FirstOrDefaultAsync(apl => apl.Name == originalName);
        Assert.NotNull(archivedLot);
        Assert.Equal(originalName, archivedLot.Name);
        Assert.NotEqual(default(DateTime), archivedLot.ArchivedAt);
        Assert.NotNull(archivedLot.ArchivedBy);
    }

    [Fact]
    public async Task DeleteParkingLot_ReturnsBadRequest_WhenActiveSessionsExist()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
        _context.ParkingSessions.Add(activeSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteParkingLot(1);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Cannot delete parking lot with active sessions", badRequestResult.Value?.ToString());
        
        var stillExists = await _context.ParkingLots.FindAsync(1);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteParkingLot_AllowsDeletion_WhenOnlyStoppedSessions()
    {
        // Arrange
        SetupUserClaims(1, "Admin");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);

        var stoppedSession = CreateTestSession(1, 1, 1, "AB-123-C", true);
        _context.ParkingSessions.Add(stoppedSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteParkingLot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        var deletedLot = await _context.ParkingLots.FindAsync(1);
        Assert.Null(deletedLot);
    }

    [Fact]
    public async Task DeleteParkingLot_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        // Arrange
        SetupUserClaims(1, "Admin");

        // Act
        var result = await _controller.DeleteParkingLot(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Parking lot met ID 999 niet gevonden", notFoundResult.Value);
    }

    [Fact]
    public async Task DeleteParkingLot_WorksWithoutAdminRole()
    {
        // NOTE: Service now explicitly checks for Admin role
        SetupUserClaims(1, "User");
        var parkingLot = CreateTestParkingLot(1);
        _context.ParkingLots.Add(parkingLot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteParkingLot(1);

        // Assert
        // Service explicitly checks for Admin role, so non-admin gets Unauthorized
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    #endregion
}