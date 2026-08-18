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
using System.Runtime.CompilerServices;

namespace MobyParkApi.Tests.Services
{
    public class ReservationServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<ReservationService>> _mockLogger;
        private readonly Mock<IArchiveService> _mockArchiveService;
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly ReservationService _service;

        public ReservationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<ReservationService>>();
            _mockArchiveService = new Mock<IArchiveService>();
            // Setup archive mock to actually remove reservation from context for tests
            _mockArchiveService
                .Setup(m => m.ArchiveReservationAsync(It.IsAny<Reservations>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<Reservations, string, string, string>(async (reservation, role, archivedBy, status) =>
                {
                    var res = await _context.Reservations.FindAsync(reservation.Id);
                    if (res != null)
                    {
                        _context.Reservations.Remove(res);
                        await _context.SaveChangesAsync();
                    }
                });
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _service = new ReservationService(_context, _mockLogger.Object, _mockArchiveService.Object, _mockDiscountCodeService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SeedDatabase()
        {
            var parkingLot = new ParkingLots
            {
                Id = 1,
                Name = "Test Parkeerplaats",
                Location = "Test Location",
                Address = "Test Straat 1",
                Capacity = 1, // Set to 1 to test availability constraints
                Reserved = 0,
                Tariff = 1.9m,
                DayTariff = 10,
                CreatedAt = DateTime.UtcNow,
                Coordinates = "{\"lat\": 52.0, \"lng\": 4.0}"
            };

            var vehicle = new Vehicles
            {
                Id = 1,
                UserId = 1,
                LicensePlate = "AB-123-CD",
                Make = "Mercedes",
                Model = "GLA",
                Color = "Red",
                Year = 2025,
                CreatedAt = DateTime.UtcNow
            };

            _context.ParkingLots.Add(parkingLot);
            _context.Vehicles.Add(vehicle);
            _context.SaveChanges();
        }

        private ReservationDto CreateValidReservationDto(
            string licensePlate = "AB-123-CD",
            int hoursFromNow = 1,
            int durationHours = 1,
            int parkingLotId = 1)
        {
            var start = DateTime.UtcNow.AddHours(hoursFromNow);
            var end = start.AddHours(durationHours);

            return new ReservationDto
            {
                LicensePlate = licensePlate,
                StartDate = start.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = end.ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = parkingLotId
            };
        }

        #endregion

        #region CreateReservationAsync Tests

        [Fact]
        public async Task CreateReservation_HappyFlow_CreatesReservationSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto();

            // Act
            var result = await _service.CreateReservationAsync(dto, 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Pending", result.Status);
            Assert.Equal("AB-123-CD", result.Vehicle.LicensePlate);
            Assert.Equal("Test Parkeerplaats", result.ParkingLot.Name);
            
            // Verify reservation was added to database
            var reservationInDb = await _context.Reservations.FirstOrDefaultAsync();
            Assert.NotNull(reservationInDb);
            Assert.Equal(1, reservationInDb.UserId);
        }

        [Fact]
        public async Task CreateReservation_BerekentKostenCorrect_1UurTijdTarief1Point9()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto(hoursFromNow: 1, durationHours: 1);

            // Act
            var result = await _service.CreateReservationAsync(dto, 1);

            // Assert
            // 1 uur * €1.90 = €1.90
            Assert.Equal(1.9m, result.Cost);
        }

        [Fact]
        public async Task CreateReservation_BerekentKostenCorrect_8UrenTijdTarief1Point9()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto(hoursFromNow: 1, durationHours: 8);

            // Act
            var result = await _service.CreateReservationAsync(dto, 1);

            // Assert
            // 8 uur * €1.90 = €15.20
            Assert.Equal(15.2m, result.Cost);
        }

        [Fact]
        public async Task CreateReservation_ParkeerplaatsBestaatNiet_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto(parkingLotId: 9999);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Parkeerplaats niet gevonden", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_VoertuigBestaatNiet_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto(licensePlate: "XX-999-YY");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Kenteken niet gevonden of niet van jou", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_VoertuigVanAndereGebruiker_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = CreateValidReservationDto();

            // Act & Assert - user 2 probeert vehicle van user 1 te gebruiken
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 2)
            );
            
            Assert.Equal("Kenteken niet gevonden of niet van jou", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_StarttijdInVerleden_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var start = DateTime.UtcNow.AddHours(-2);
            var end = DateTime.UtcNow.AddHours(-1);
            
            var dto = new ReservationDto
            {
                LicensePlate = "AB-123-CD",
                StartDate = start.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = end.ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = 1
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Starttijd moet in de toekomst zijn", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_EindtijdVoorStarttijd_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var start = DateTime.UtcNow.AddHours(2);
            var end = DateTime.UtcNow.AddHours(1);
            
            var dto = new ReservationDto
            {
                LicensePlate = "AB-123-CD",
                StartDate = start.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = end.ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = 1
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Eindtijd moet na starttijd zijn", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_GeenBeschikbaarheid_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            
            // Vul alle 10 plekken
            var start = DateTime.UtcNow.AddHours(1);
            var end = start.AddHours(1);
            
            for (int i = 0; i < 10; i++)
            {
                _context.Reservations.Add(new Reservations
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    VehicleId = 1,
                    StartTime = start,
                    EndTime = end,
                    Status = "Confirmed",
                    Cost = 1.9m,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            var dto = CreateValidReservationDto(hoursFromNow: 1, durationHours: 1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Geen beschikbare plekken in deze periode", exception.Message);
        }

        [Fact]
        public async Task CreateReservation_OngeldigDatumFormaat_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var dto = new ReservationDto
            {
                LicensePlate = "AB-123-CD",
                StartDate = "invalid-date",
                EndDate = "also-invalid",
                ParkingLotId = 1
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateReservationAsync(dto, 1)
            );
            
            Assert.Equal("Ongeldig datum formaat. Gebruik YYYY-MM-DD HH:MM:SS", exception.Message);
        }

        #endregion

        #region GetReservationByIdAsync Tests

        [Fact]
        public async Task GetReservationById_EigenReservering_ReturnsReservation()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetReservationByIdAsync(1, 1, "User");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task GetReservationById_ReserveringVanAndereGebruiker_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.GetReservationByIdAsync(1, 2, "User")
            );
            
            Assert.Equal("Je hebt geen toegang tot deze reservering", exception.Message);
        }

        [Fact]
        public async Task GetReservationById_AdminKanAlleReserveringenZien_ReturnsReservation()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act - Admin (userId 99) bekijkt reservering van user 1
            var result = await _service.GetReservationByIdAsync(1, 99, "Admin");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Theory]
        [InlineData("admin")]  // lowercase
        [InlineData("ADMIN")]  // uppercase
        [InlineData("AdMiN")]  // mixed case
        public async Task GetReservationById_CaseInsensitiveAdminCheck_ReturnsReservation(string adminRole)
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetReservationByIdAsync(1, 99, adminRole);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetReservationById_ReserveringBestaatNiet_ReturnsNull()
        {
            // Arrange
            SeedDatabase();

            // Act
            var result = await _service.GetReservationByIdAsync(9999, 1, "User");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region UpdateReservationAsync Tests

        [Fact]
        public async Task UpdateReservation_SuccesvolUpdaten_UpdatesReservation()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var dto = CreateValidReservationDto(hoursFromNow: 2, durationHours: 3);

            // Act
            var result = await _service.UpdateReservationAsync(1, dto, 1, "User");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Confirmed", result.Status);
            // 3 uur * €1.90 = €5.70
            Assert.Equal(5.7m, result.Cost);
        }

        [Fact]
        public async Task UpdateReservation_ReserveringVanAndereGebruiker_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var dto = CreateValidReservationDto();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.UpdateReservationAsync(1, dto, 2, "User")
            );
            
            Assert.Equal("Je hebt geen toegang tot deze reservering", exception.Message);
        }

        [Fact]
        public async Task UpdateReservation_GeenBeschikbaarheid_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            
            // Bestaande reservering
            var existingReservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(existingReservation);

            // Vul alle andere plekken
            var start = DateTime.UtcNow.AddHours(3);
            var end = start.AddHours(1);
            
            for (int i = 2; i < 12; i++) // 10 plekken gevuld
            {
                _context.Reservations.Add(new Reservations
                {
                    Id = i,
                    UserId = 1,
                    ParkingLotId = 1,
                    VehicleId = 1,
                    StartTime = start,
                    EndTime = end,
                    Status = "Confirmed",
                    Cost = 1.9m,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            // Probeer reservering 1 te updaten naar dezelfde tijd als de volle periode
            var dto = CreateValidReservationDto(hoursFromNow: 3, durationHours: 1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdateReservationAsync(1, dto, 1, "User")
            );
            
            Assert.Equal("Geen beschikbare plekken in deze periode", exception.Message);
        }

        #endregion

        #region DeleteReservationAsync Tests

        [Fact]
        public async Task DeleteReservation_SuccesvolVerwijderen_DeletesReservation()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act
            await _service.DeleteReservationAsync(1, 1, "User", "testuser");

            // Assert
            var deletedReservation = await _context.Reservations.FindAsync(1);
            Assert.Null(deletedReservation);
        }

        [Fact]
        public async Task DeleteReservation_ReserveringVanAndereGebruiker_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.DeleteReservationAsync(1, 2, "User", "testuser2")
            );
            
            Assert.Equal("Je hebt geen toegang tot deze reservering", exception.Message);
        }

        [Fact]
        public async Task DeleteReservation_AdminKanAlleReserveringenVerwijderen_DeletesReservation()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Act - Admin verwijdert reservering van user 1
            await _service.DeleteReservationAsync(1, 99, "Admin", "adminuser");


            // Assert
            var deletedReservation = await _context.Reservations.FindAsync(1);
            Assert.Null(deletedReservation);
        }

        [Fact]
        public async Task DeleteReservation_ReserveringBestaatNiet_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.DeleteReservationAsync(9999, 1, "User", "testuser")
            );
            
            Assert.Equal("Reservering niet gevonden", exception.Message);
        }

        #endregion

        #region GetAllUserReservationsAsync Tests

        [Fact]
        public async Task GetAllUserReservations_ReturnsOnlyUserReservations()
        {
            // Arrange
            SeedDatabase();
            
            // Reserveringen van user 1
            _context.Reservations.Add(new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            });
            
            _context.Reservations.Add(new Reservations
            {
                Id = 2,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(3),
                EndTime = DateTime.UtcNow.AddHours(4),
                Status = "Confirmed",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            });
            
            // Reservering van user 2
            _context.Reservations.Add(new Reservations
            {
                Id = 3,
                UserId = 2,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(5),
                EndTime = DateTime.UtcNow.AddHours(6),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllUserReservationsAsync(1);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.Equal(1, r.Vehicle.Id)); // Indirect check via vehicle
        }

        [Fact]
        public async Task GetAllUserReservations_GeenReserveringen_ReturnsEmptyList()
        {
            // Arrange
            SeedDatabase();

            // Act
            var result = await _service.GetAllUserReservationsAsync(1);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region UpdateReservationTimeAsync Tests

        [Fact]
        public async Task UpdateReservationTime_ValidUpdate_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act
            var result = await _service.UpdateReservationTimeAsync(1, updateDto, 1, "User");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3.8m, result.Cost); // 2 hours * 1.9
        }

        [Fact]
        public async Task UpdateReservationTime_NonExistentReservation_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateReservationTimeAsync(999, updateDto, 1, "User"));
        }

        [Fact]
        public async Task UpdateReservationTime_OtherUserReservation_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 2, // Different user
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateReservationTimeAsync(1, updateDto, 1, "User"));
        }

        [Fact]
        public async Task UpdateReservationTime_AdminCanUpdateAnyReservation_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 2, // Different user
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act
            var result = await _service.UpdateReservationTimeAsync(1, updateDto, 99, "Admin");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateReservationTime_StartTimeInPast_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateReservationTimeAsync(1, updateDto, 1, "User"));
            Assert.Contains("Starttijd moet in de toekomst zijn", exception.Message);
        }

        [Fact]
        public async Task UpdateReservationTime_EndTimeBeforeStartTime_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") // Before start
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateReservationTimeAsync(1, updateDto, 1, "User"));
            Assert.Contains("Eindtijd moet na starttijd zijn", exception.Message);
        }

        [Fact]
        public async Task UpdateReservationTime_NoAvailability_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            // Parking lot has capacity of 1, so we need to create an overlapping reservation
            var existingReservation = new Reservations
            {
                Id = 2,
                UserId = 2,
                ParkingLotId = 1,
                VehicleId = 2,
                StartTime = DateTime.UtcNow.AddHours(3),
                EndTime = DateTime.UtcNow.AddHours(5),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(existingReservation);

            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationTimeDto
            {
                StartDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Act & Assert
            // Parking lot capacity is 1, and there's already a reservation at that time
            // But UpdateReservationTimeAsync excludes the current reservation (id=1) from availability check
            // So it should succeed because there's 1 spot available (capacity 1 - 0 overlapping excluding current)
            // To make it fail, we need capacity 1 and 1 other reservation overlapping
            // Actually, the existing reservation overlaps, so capacity 1 - 1 overlapping = 0 available
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateReservationTimeAsync(1, updateDto, 1, "User"));
            Assert.Contains("Geen beschikbare plekken", exception.Message);
        }

        #endregion

        #region UpdateReservationVehicleAsync Tests

        [Fact]
        public async Task UpdateReservationVehicle_ValidUpdate_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var vehicle2 = new Vehicles
            {
                Id = 2,
                UserId = 1,
                LicensePlate = "CD-456-E",
                Make = "Honda",
                Model = "Civic",
                Color = "Rood",
                Year = 2021,
                CreatedAt = DateTime.UtcNow
            };
            _context.Vehicles.Add(vehicle2);

            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "CD-456-E"
            };

            // Act
            var result = await _service.UpdateReservationVehicleAsync(1, updateDto, 1, "User");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Vehicle.Id);
        }

        [Fact]
        public async Task UpdateReservationVehicle_NonExistentReservation_ThrowsKeyNotFoundException()
        {
            // Arrange
            SeedDatabase();
            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "AB-123-CD"
            };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateReservationVehicleAsync(999, updateDto, 1, "User"));
        }

        [Fact]
        public async Task UpdateReservationVehicle_OtherUserReservation_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            SeedDatabase();
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 2, // Different user
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "AB-123-CD"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateReservationVehicleAsync(1, updateDto, 1, "User"));
        }

        [Fact]
        public async Task UpdateReservationVehicle_VehicleNotOwnedByUser_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var vehicle2 = new Vehicles
            {
                Id = 2,
                UserId = 2, // Different user
                LicensePlate = "CD-456-E",
                Make = "Honda",
                Model = "Civic",
                Color = "Rood",
                Year = 2021,
                CreatedAt = DateTime.UtcNow
            };
            _context.Vehicles.Add(vehicle2);

            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "CD-456-E"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateReservationVehicleAsync(1, updateDto, 1, "User"));
            Assert.Contains("Kenteken niet gevonden of niet van jou", exception.Message);
        }

        [Fact]
        public async Task UpdateReservationVehicle_VehicleNotAvailable_ThrowsArgumentException()
        {
            // Arrange
            SeedDatabase();
            var vehicle2 = new Vehicles
            {
                Id = 2,
                UserId = 1,
                LicensePlate = "CD-456-E",
                Make = "Honda",
                Model = "Civic",
                Color = "Rood",
                Year = 2021,
                CreatedAt = DateTime.UtcNow
            };
            _context.Vehicles.Add(vehicle2);

            // Create overlapping reservation for vehicle2
            var overlappingReservation = new Reservations
            {
                Id = 2,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 2,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(overlappingReservation);

            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "CD-456-E"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateReservationVehicleAsync(1, updateDto, 1, "User"));
            Assert.Contains("niet beschikbaar", exception.Message);
        }

        [Fact]
        public async Task UpdateReservationVehicle_AdminCanUpdateAnyReservation_UpdatesSuccessfully()
        {
            // Arrange
            SeedDatabase();
            var vehicle2 = new Vehicles
            {
                Id = 2,
                UserId = 2,
                LicensePlate = "CD-456-E",
                Make = "Honda",
                Model = "Civic",
                Color = "Rood",
                Year = 2021,
                CreatedAt = DateTime.UtcNow
            };
            _context.Vehicles.Add(vehicle2);

            var reservation = new Reservations
            {
                Id = 1,
                UserId = 2, // Different user
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                Status = "Pending",
                Cost = 1.9m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateReservationVehicleDto
            {
                LicensePlate = "CD-456-E"
            };

            // Act - Admin updates reservation of user 2, but uses vehicle of user 2
            var result = await _service.UpdateReservationVehicleAsync(1, updateDto, 2, "Admin");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Vehicle.Id);
        }

        #endregion
    }
}