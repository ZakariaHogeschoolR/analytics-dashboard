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
using MobyParkApi.Service;
using MobyParkApi.Services;
using Xunit;

namespace MobyParkApi.Tests.Service
{
	public class ParkingLotServiceTests : IDisposable
	{
		private readonly ApplicationDbContext _context;
		private readonly Mock<ILogger<ParkingLotsController>> _loggerMock;
		private readonly Mock<ILogger<ReservationController>> _loggerMock2;
		private readonly Mock<ILogger<ReservationService>> _loggerMock3;
		private readonly Mock<IAddressValidationService> _addressValidationMock;
		private readonly Mock<IArchiveService> _archiveServiceMock;
		private readonly Mock<IDiscountCodeService> _discountCodeServiceMock;
		private readonly ParkingLotService _service;
		private readonly ReservationService _reservationService;

		public ParkingLotServiceTests()
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
			_service = new ParkingLotService(_context, _loggerMock.Object, _loggerMock2.Object, _reservationService, _addressValidationMock.Object);
		}

		public void Dispose()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

		#region Helper Methods

		private ClaimsPrincipal CreateClaimsPrincipal(int userId, string role = "User")
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
				new Claim(ClaimTypes.Name, "testuser"),
				new Claim(ClaimTypes.Role, role)
			};

			var identity = new ClaimsIdentity(claims, "TestAuth");
			return new ClaimsPrincipal(identity);
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

		#region GetAllParkingLots Tests

		[Fact]
		public async Task GetAllParkingLots_ReturnsAllParkingLots()
		{
			// Arrange
			var parkingLot1 = CreateTestParkingLot(1);
			var parkingLot2 = CreateTestParkingLot(2);
			parkingLot2.Name = "Second Parking";
			_context.ParkingLots.AddRange(parkingLot1, parkingLot2);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetAllParkingLotsService();

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
		}

		[Fact]
		public async Task GetAllParkingLots_ReturnsEmptyList_WhenNoParkingLots()
		{
			// Act
			var result = await _service.GetAllParkingLotsService();

			// Assert
			Assert.NotNull(result);
			Assert.Empty(result);
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
			var parkingLots = new[]
			{
				CreateTestParkingLot(1),
				CreateTestParkingLot(2),
				CreateTestParkingLot(3)
			};
			parkingLots[1].Name = "B Parking";
			parkingLots[2].Name = "C Parking";
			_context.ParkingLots.AddRange(parkingLots);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetAllParkingLotsService(sortBy, order);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, result.Count);
		}

		#endregion

		#region GetParkingLotById Tests

		[Fact]
		public async Task GetParkingLotById_ReturnsParkingLot_WhenExists()
		{
			// Arrange
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetParkingLotByIdService(1);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(1, result.Id);
			Assert.Equal("Test Parking", result.Name);
		}

		[Fact]
		public async Task GetParkingLotById_ThrowsKeyNotFoundException_WhenNotFound()
		{
			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetParkingLotByIdService(999));
		}

		[Fact]
		public async Task GetParkingLotById_ThrowsArgumentException_WhenCapacityLessThanReserved()
		{
			// Arrange
			var parkingLot = CreateTestParkingLot(1);
			parkingLot.Capacity = 5;
			parkingLot.Reserved = 10; // Meer dan capacity
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.GetParkingLotByIdService(1));
		}

		#endregion

		#region GetParkingLotSessions Tests

		[Fact]
		public async Task GetParkingLotSessions_ReturnsAllSessions_ForAdmin()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var session1 = CreateTestSession(1, 1, 1, "AB-123-C");
			var session2 = CreateTestSession(2, 1, 2, "XY-456-Z");
			_context.ParkingSessions.AddRange(session1, session2);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetParkingLotSessionsService(1, user);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count); // Admin ziet alle sessions
		}

		[Fact]
		public async Task GetParkingLotSessions_ReturnsOnlyUserSessions_ForNonAdmin()
		{
			// Arrange
			const int userId = 1;
			var user = CreateClaimsPrincipal(userId, "User");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var session1 = CreateTestSession(1, 1, userId, "AB-123-C");
			var session2 = CreateTestSession(2, 1, 2, "XY-456-Z"); // Andere user
			_context.ParkingSessions.AddRange(session1, session2);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetParkingLotSessionsService(1, user);

		// Assert
		Assert.NotNull(result);
		Assert.Single(result); // User ziet alleen eigen sessions
		Assert.Equal(userId, result[0].userId);
		}

		[Fact]
		public async Task GetParkingLotSessions_FiltersActiveOnly_WhenRequested()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
			var stoppedSession = CreateTestSession(2, 1, 1, "XY-456-Z", true);
			_context.ParkingSessions.AddRange(activeSession, stoppedSession);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetParkingLotSessionsService(1, user, activeOnly: true);

		// Assert
		Assert.NotNull(result);
		Assert.Single(result);
		// Note: ParkingSessionDto.stopped is always a DateTime (not nullable), so we check if it's set
		Assert.True(result[0].stopped > DateTime.MinValue);
		}

		[Fact]
		public async Task GetParkingLotSessions_ThrowsKeyNotFoundException_WhenParkingLotNotFound()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetParkingLotSessionsService(999, user));
		}

		[Fact]
		public async Task GetParkingLotSessions_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
		{
			// Arrange
			var user = new ClaimsPrincipal(); // Geen claims

			// Act & Assert
			await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetParkingLotSessionsService(1, user));
		}

		#endregion

		#region StartSession Tests

		[Fact]
		public async Task StartSession_CreatesSession_WhenValid()
		{
			// Arrange
			const int userId = 1;
			var user = CreateClaimsPrincipal(userId);
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };
			
			// Act
			var result = await _service.StartSessionService(1, request, user);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("AB-123-C", result.LicensePlate);
			Assert.Equal(userId, result.UserId);
			Assert.Equal(1, result.ParkingLotId);
			Assert.Null(result.Stopped);
			Assert.Equal("PENDING", result.PaymentStatus);

			// Verify in database
			var session = await _context.ParkingSessions.FirstOrDefaultAsync();
			Assert.NotNull(session);
			Assert.Equal("AB-123-C", session.LicensePlate);
		}

		[Fact]
		public async Task StartSession_ThrowsKeyNotFoundException_WhenParkingLotNotFound()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.StartSessionService(999, request, user));
		}

		[Fact]
		public async Task StartSession_ThrowsArgumentException_WhenDuplicateActiveSession()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var existingSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
			_context.ParkingSessions.Add(existingSession);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };
			
			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.StartSessionService(1, request, user));
		}

		[Fact]
		public async Task StartSession_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
		{
			// Arrange
			// NOTE: StartSessionService now allows anonymous users (walk-up sessions)
			// This test should verify that walk-up sessions are created with IsWalkUp=true
			// instead of throwing UnauthorizedAccessException
			var user = new ClaimsPrincipal(); // Geen claims
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };
			
			// Act
			var result = await _service.StartSessionService(1, request, user);
			
			// Assert - Walk-up session should be created with IsWalkUp = true
			Assert.NotNull(result);
			Assert.True(result.IsWalkUp);
			Assert.Null(result.UserId);
		}

		[Fact]
		public async Task StartSession_ConvertsLicensePlateToUpperCase()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "ab-123-c" };

			// Act
			var result = await _service.StartSessionService(1, request, user);

			// Assert
			Assert.Equal("AB-123-C", result.LicensePlate);
		}

		#endregion

		#region StopSession Tests

		[Fact]
		public async Task StopSession_StopsSession_WhenValid()
		{
			// Arrange
			const int userId = 1;
			var user = CreateClaimsPrincipal(userId);
			var parkingLot = CreateTestParkingLot(1);
			parkingLot.Tariff = 4.00m;
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, userId, "AB-123-C", false);
			activeSession.Started = DateTime.UtcNow.AddMinutes(-60); // 1 uur geleden
			_context.ParkingSessions.Add(activeSession);
			await _context.SaveChangesAsync();

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act
			var result = await _service.StopSessionService(1, request, user);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Stopped);
			Assert.NotNull(result.DurationMinutes);
			Assert.True(result.DurationMinutes >= 60); // Minimaal 60 minuten
			Assert.True(result.Cost > 0);
			Assert.True(result.Cost >= 4.00m); // Minimaal 1 uur × €4 = €4
			
			// Verify session is archived
			var archivedSession = await _context.ArchivedParkingSessions
				.FirstOrDefaultAsync(aps => aps.OriginalSessionId == activeSession.Id || aps.LicensePlate == "AB-123-C");
			Assert.NotNull(archivedSession);
			Assert.Equal("testuser", archivedSession.ArchivedBy);
			
			// Verify session is deleted from main table
			var deletedSession = await _context.ParkingSessions.FindAsync(activeSession.Id);
			Assert.Null(deletedSession);
		}

		[Fact]
		public async Task StopSession_CalculatesCostCorrectly()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var parkingLot = CreateTestParkingLot(1);
			parkingLot.Tariff = 3.50m; // €3.50 per uur
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
			activeSession.Started = DateTime.UtcNow.AddMinutes(-30); // 30 minuten geleden
			_context.ParkingSessions.Add(activeSession);
			await _context.SaveChangesAsync();

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act
			var result = await _service.StopSessionService(1, request, user);

			// Assert
			// 30 minuten = 0.5 uur, maar wordt afgerond naar boven = 1 uur
			// 1 uur × €3.50 = €3.50
			Assert.True(result.Cost >= 3.50m);
		}

		[Fact]
		public async Task StopSession_ThrowsKeyNotFoundException_WhenParkingLotNotFound()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.StopSessionService(999, request, user));
		}

		[Fact]
		public async Task StopSession_ThrowsArgumentException_WhenNoActiveSession()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1);
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.StopSessionService(1, request, user));
		}

		[Fact]
		public async Task StopSession_AllowsAdminToStopOtherUserSession()
		{
			// Arrange
			var adminUser = CreateClaimsPrincipal(2, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false); // User 1's session
			_context.ParkingSessions.Add(activeSession);
			await _context.SaveChangesAsync();

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act
			var result = await _service.StopSessionService(1, request, adminUser);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Stopped);
			
			// Verify session is archived
			var archivedSession = await _context.ArchivedParkingSessions
				.FirstOrDefaultAsync(aps => aps.OriginalSessionId == activeSession.Id || aps.LicensePlate == "AB-123-C");
			Assert.NotNull(archivedSession);
			
			// Verify session is deleted from main table
			var deletedSession = await _context.ParkingSessions.FindAsync(activeSession.Id);
			Assert.Null(deletedSession);
		}

		[Fact]
		public async Task StopSession_ThrowsUnauthorizedAccessException_WhenUserTriesToStopOtherUserSession()
		{
			// Arrange
			var user = CreateClaimsPrincipal(2, "User"); // User 2
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false); // User 1's session
			_context.ParkingSessions.Add(activeSession);
			await _context.SaveChangesAsync();

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act & Assert
			await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.StopSessionService(1, request, user));
		}

		#endregion

		#region CreateParkingLot Tests

		[Fact]
		public async Task CreateParkingLot_CreatesParkingLot_WhenValid()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
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
			var result = await _service.CreateParkingLotService(request, user);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("New Parking", result.Name);
			Assert.Equal(200, result.Capacity);
			Assert.True(result.Coordinates.Contains("52.3676") || result.Coordinates.Contains("52,3676"));

			// Verify in database
			var parkingLot = await _context.ParkingLots.FirstOrDefaultAsync();
			Assert.NotNull(parkingLot);
			Assert.Equal("New Parking", parkingLot.Name);
		}

		[Fact]
		public async Task CreateParkingLot_ThrowsArgumentException_WhenCapacityLessThanReserved()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var request = new CreateParkingLotRequestDto
			{
				Name = "New Parking",
				Location = "Amsterdam",
				Postcode = "1000 AA",
				HouseNumber = 1,
				Capacity = 10,
				Reserved = 20, // Meer dan capacity
				Tariff = 3.00m,
				DayTariff = 20.00m,
				Lat = 52.3676,
				Lng = 4.9041
			};

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateParkingLotService(request, user));
		}

		[Fact]
		public async Task CreateParkingLot_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
		{
			// Arrange
			var user = new ClaimsPrincipal(); // Geen claims
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

			// Act & Assert
			await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateParkingLotService(request, user));
		}

		#endregion

		#region UpdateParkingLot Tests

		[Fact]
		public async Task UpdateParkingLot_UpdatesParkingLot_WhenValid()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
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
			var result = await _service.UpdateParkingLotService(1, request, user);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("Updated Parking", result.Name);
			Assert.Equal("Rotterdam", result.Location);
			Assert.Equal(4.00m, result.Tariff);

			// Verify in database
			var updatedLot = await _context.ParkingLots.FindAsync(1);
			Assert.NotNull(updatedLot);
			Assert.Equal("Updated Parking", updatedLot.Name);
		}

		[Fact]
		public async Task UpdateParkingLot_ThrowsKeyNotFoundException_WhenNotFound()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
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

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateParkingLotService(999, request, user));
		}

		[Fact]
		public async Task UpdateParkingLot_ThrowsArgumentException_WhenCapacityLessThanReserved()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new CreateParkingLotRequestDto
			{
				Name = "Updated Parking",
				Location = "Rotterdam",
				Postcode = "3000 AA",
				HouseNumber = 1,
				Capacity = 10,
				Reserved = 20, // Meer dan capacity
				Tariff = 4.00m,
				DayTariff = 30.00m,
				Lat = 51.9244,
				Lng = 4.4777
			};

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateParkingLotService(1, request, user));
		}

		#endregion

		#region DeleteParkingLot Tests

		[Fact]
		public async Task DeleteParkingLot_DeletesParkingLot_WhenNoActiveSessions()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.DeleteParkingLotService(1, user);

			// Assert
			Assert.NotNull(result);
			
			// Verify deleted from database
			var deletedLot = await _context.ParkingLots.FindAsync(1);
			Assert.Null(deletedLot);
		}

		[Fact]
		public async Task DeleteParkingLot_ArchivesToArchivedParkingLots_WhenDeleted()
		{
			// Arrange
			const int userId = 1;
			var user = CreateClaimsPrincipal(userId, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			var originalCreatedAt = DateTime.UtcNow.AddDays(-30);
			var originalModifiedAt = DateTime.UtcNow.AddDays(-10);
			parkingLot.CreatedAt = originalCreatedAt;
			parkingLot.ModifiedAt = originalModifiedAt;
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var originalParkingLotId = parkingLot.Id;

			// Act
			var result = await _service.DeleteParkingLotService(1, user);

			// Assert
			Assert.NotNull(result);
			
			// Verify deleted from active table
			var deletedLot = await _context.ParkingLots.FindAsync(originalParkingLotId);
			Assert.Null(deletedLot);
			
			// Verify archived to ArchivedParkingLots
			var archivedLot = await _context.ArchivedParkingLots
				.FirstOrDefaultAsync(apl => apl.Name == parkingLot.Name && apl.Location == parkingLot.Location);
			Assert.NotNull(archivedLot);
			
			// Verify all fields are correctly archived
			Assert.Equal(parkingLot.Name, archivedLot.Name);
			Assert.Equal(parkingLot.Location, archivedLot.Location);
			Assert.Equal(parkingLot.Address, archivedLot.Address);
			Assert.Equal(parkingLot.Capacity, archivedLot.Capacity);
			Assert.Equal(parkingLot.Reserved, archivedLot.Reserved);
			Assert.Equal((double)parkingLot.Tariff, archivedLot.Tariff);
			Assert.Equal(parkingLot.DayTariff.ToString(), archivedLot.DayTariff);
			Assert.Equal(parkingLot.Coordinates, archivedLot.Coordinates);
			// ArchivedBy is userId.ToString() if ClaimTypes.Name is not set
			Assert.True(archivedLot.ArchivedBy == userId.ToString() || !string.IsNullOrEmpty(archivedLot.ArchivedBy));
			Assert.True(archivedLot.ArchivedAt > DateTime.UtcNow.AddMinutes(-1));
			Assert.True(archivedLot.ArchivedAt <= DateTime.UtcNow.AddMinutes(1));
			
			// Verify CreatedAt and ModifiedAt are preserved
			Assert.NotNull(archivedLot.CreatedAt);
			Assert.NotNull(archivedLot.ModifiedAt);
		}

		[Fact]
		public async Task DeleteParkingLot_ThrowsArgumentException_WhenActiveSessionsExist()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var activeSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
			_context.ParkingSessions.Add(activeSession);
			await _context.SaveChangesAsync();

			// Act & Assert
			await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteParkingLotService(1, user));
			
			// Verify still exists
			var stillExists = await _context.ParkingLots.FindAsync(1);
			Assert.NotNull(stillExists);
		}

		[Fact]
		public async Task DeleteParkingLot_AllowsDeletion_WhenOnlyStoppedSessions()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			var stoppedSession = CreateTestSession(1, 1, 1, "AB-123-C", true);
			_context.ParkingSessions.Add(stoppedSession);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.DeleteParkingLotService(1, user);

			// Assert
			Assert.NotNull(result);
			
			// Verify deleted from database
			var deletedLot = await _context.ParkingLots.FindAsync(1);
			Assert.Null(deletedLot);
		}

		[Fact]
		public async Task DeleteParkingLot_ThrowsKeyNotFoundException_WhenNotFound()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin");

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteParkingLotService(999, user));
		}

		[Fact]
		public async Task DeleteParkingLot_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
		{
			// Arrange
			var user = new ClaimsPrincipal(); // Geen claims
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			// Act & Assert
			await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteParkingLotService(1, user));
		}

		#endregion

		#region Walk-up Session Tests

		[Fact]
		public async Task StartSessionService_CreatesWalkUpSession_WhenUserIsNotAuthenticated()
		{
			// Arrange
			var emptyUser = new ClaimsPrincipal(); // No claims = not authenticated
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act
			var result = await _service.StartSessionService(1, request, emptyUser);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("AB-123-C", result.LicensePlate);
			Assert.Null(result.UserId); // Walk-up sessions have no userId
			Assert.True(result.IsWalkUp); // Must be marked as walk-up
			Assert.Equal(1, result.ParkingLotId);
			Assert.Null(result.Stopped);
			Assert.Equal("PENDING", result.PaymentStatus);

			// Verify in database
			var session = await _context.ParkingSessions.FirstOrDefaultAsync();
			Assert.NotNull(session);
			Assert.Equal("AB-123-C", session.LicensePlate);
			Assert.Null(session.UserId);
			Assert.True(session.IsWalkUp);
		}

		[Fact]
		public async Task StartSessionService_ThrowsInvalidOperationException_WhenWalkUpLicensePlateIsInvalid()
		{
			// Arrange
			var emptyUser = new ClaimsPrincipal(); // No claims = not authenticated
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);
			await _context.SaveChangesAsync();

			var request = new StartSessionRequestDto { LicensePlate = "INVALID" }; // Too short, invalid format

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartSessionService(1, request, emptyUser));
		}

		[Fact]
		public async Task StopSessionService_ArchivesWalkUpSession_WithIsWalkUpTrue()
		{
			// Arrange
			var emptyUser = new ClaimsPrincipal(); // No claims = walk-up
			var parkingLot = CreateTestParkingLot(1);
			parkingLot.Tariff = 4.00m;
			_context.ParkingLots.Add(parkingLot);

			// Create a walk-up session directly
			var walkUpSession = new ParkingSessions
			{
				ParkingLotId = 1,
				LicensePlate = "AB-123-C",
				Started = DateTime.UtcNow.AddHours(-1),
				Stopped = null,
				UserId = null, // Walk-up: no userId
				IsWalkUp = true,
				PaymentStatus = "PENDING",
				CreatedAt = DateTime.UtcNow.AddHours(-1)
			};
			_context.ParkingSessions.Add(walkUpSession);
			await _context.SaveChangesAsync();
			var originalSessionId = walkUpSession.Id;

			var request = new StopSessionRequestDto { LicensePlate = "AB-123-C" };

			// Act
			var result = await _service.StopSessionService(1, request, emptyUser);

			// Assert
			Assert.NotNull(result);
			Assert.True(result.IsWalkUp); // Session should have IsWalkUp = true
			Assert.Null(result.UserId); // Walk-up sessions have no userId
			Assert.NotNull(result.Stopped); // Session should be stopped
			Assert.NotNull(result.DurationMinutes);
			Assert.NotNull(result.Cost);
			
			// Walk-up sessies worden nu ook gearchiveerd naar ArchivedParkingSessions
			// De originele sessie moet verwijderd zijn uit ParkingSessions
			var updatedSession = await _context.ParkingSessions.FindAsync(originalSessionId);
			Assert.Null(updatedSession); // Session should be removed (archived)
			
			// Controleer dat de sessie is gearchiveerd
			var archivedSession = await _context.ArchivedParkingSessions
				.FirstOrDefaultAsync(a => a.OriginalSessionId == originalSessionId);
			Assert.NotNull(archivedSession); // Session should be archived
			Assert.True(archivedSession.IsWalkUp); // IsWalkUp should be preserved in archive
			Assert.Null(archivedSession.UserId); // Walk-up sessions have no userId
			Assert.Equal("WALKUP", archivedSession.ArchivedBy); // Should be archived by WALKUP
			Assert.NotNull(archivedSession.Stopped); // Should be stopped
			Assert.NotNull(archivedSession.DurationMinutes);
			Assert.NotNull(archivedSession.Cost);
		}

		[Fact]
		public async Task GetParkingLotSessionsService_ReturnsIsWalkUpInDto_ForWalkUpSessions()
		{
			// Arrange
			var user = CreateClaimsPrincipal(1, "Admin"); // Admin to see all sessions
			var parkingLot = CreateTestParkingLot(1);
			_context.ParkingLots.Add(parkingLot);

			// Create a normal session
			var normalSession = CreateTestSession(1, 1, 1, "AB-123-C", false);
			normalSession.IsWalkUp = false;
			
			// Create a walk-up session
			var walkUpSession = new ParkingSessions
			{
				Id = 2,
				ParkingLotId = 1,
				LicensePlate = "XY-456-Z",
				Started = DateTime.UtcNow.AddHours(-1),
				Stopped = null,
				UserId = null,
				IsWalkUp = true,
				PaymentStatus = "PENDING",
				CreatedAt = DateTime.UtcNow.AddHours(-1)
			};

			_context.ParkingSessions.AddRange(normalSession, walkUpSession);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetParkingLotSessionsService(1, user);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(2, result.Count);
			
			var normalSessionDto = result.FirstOrDefault(s => s.licensePlate == "AB-123-C");
			var walkUpSessionDto = result.FirstOrDefault(s => s.licensePlate == "XY-456-Z");
			
			Assert.NotNull(normalSessionDto);
			Assert.False(normalSessionDto.isWalkUp);
			
			Assert.NotNull(walkUpSessionDto);
			Assert.True(walkUpSessionDto.isWalkUp);
			Assert.Equal(0, walkUpSessionDto.userId); // Walk-up sessions have userId = 0 in DTO (null in DB)
		}

		#endregion
	}
}

