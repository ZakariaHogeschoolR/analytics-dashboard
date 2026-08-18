using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using MobyParkApi.Service;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Text.Json;

namespace MobyParkApi.Tests.Integration
{
    public class ArchiveControllerIntegrationTest
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private ControllerContext GetControllerContext(int userId, string username, string role = "User")
        {
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Name, username),
                        new Claim(ClaimTypes.Role, role)
                    }, "TestAuth"))
                }
            };
        }
        [Fact]
        public async Task DeleteReservation_ShouldArchiveReservation_ForAdmin()
        {
            // Arrange
            var loggerMocks = new
            {
                ParkingLot = new Mock<ILogger<ParkingLotsController>>(),
                Reservation = new Mock<ILogger<ReservationController>>(),
                ReservationService = new Mock<ILogger<ReservationService>>(),
                ArchiveService = new Mock<ILogger<ArchiveService>>(),
                Vehicle = new Mock<ILogger<VehiclesController>>(),
                AutoComplete = new Mock<ILogger<ReservationAutoCompleteService>>(),
                PaymentService = new Mock<ILogger<PaymentGenerationService>>(),
                DiscountCode  = new Mock<ILogger<DiscountCodeService>>(),
                kadaster = new Mock<ILogger<KadasterAddressValidationService>>(),
                addressValidationMock = new Mock<IAddressValidationService>()
            };
            // Mock zodat elk adres geldig is tijdens tests
            loggerMocks.addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });
            await using var dbContext = GetInMemoryDbContext("TestDb_ArchiveReservations");
            dbContext.Database.EnsureDeleted();
            // Services
            var discountService = new DiscountCodeService(dbContext, loggerMocks.DiscountCode.Object);
            var archiveService = new ArchiveService(dbContext, loggerMocks.ArchiveService.Object);
            var reservationService = new ReservationService(dbContext, loggerMocks.ReservationService.Object, archiveService, discountService);
            var paymentService = new PaymentGenerationService(dbContext, loggerMocks.PaymentService.Object);
            var autoCompleteService = new ReservationAutoCompleteService(dbContext, paymentService, loggerMocks.AutoComplete.Object);
            // Test user
            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };
            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();
            var userId = testUser.Id;

            // Controllers
            var parkingLotController = new ParkingLotsController(dbContext, loggerMocks.ParkingLot.Object, loggerMocks.Reservation.Object, reservationService, loggerMocks.addressValidationMock.Object)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var vehicleController = new VehiclesController(dbContext, loggerMocks.Vehicle.Object, archiveService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var reservationController = new ReservationController(reservationService, dbContext, loggerMocks.Reservation.Object, autoCompleteService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            // Create vehicle
            var vehicleDto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-P",
                Make = "TestMakeS",
                Model = "TestModelS",
                Color = "GradientBlue",
                Year = 1999
            };
            var vehicleResult = await vehicleController.CreateVehicle(vehicleDto);
            vehicleResult.Should().BeOfType<OkObjectResult>();
            var createdVehicle = ((OkObjectResult)vehicleResult).Value as Vehicles;
            createdVehicle.Should().NotBeNull();

            // Create parking lot
            var parkingLotDto = new CreateParkingLotRequestDto
            {
                Name = "testtest",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 101,
                Reserved = 11,
                Tariff = 3.5m,
                DayTariff = 21.0m,
                Location = "Testtest Location",
                Lat = 53.5,
                Lng = 5.89
            };
            var parkingLotResult = await parkingLotController.CreateParkingLot(parkingLotDto);
            parkingLotResult.Should().BeOfType<CreatedAtActionResult>();
            var createdParkingLot = ((CreatedAtActionResult)parkingLotResult).Value
                .GetType().GetProperty("parkingLot")?.GetValue(((CreatedAtActionResult)parkingLotResult).Value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            // Create reservation
            var reservationDto = new ReservationDto
            {
                LicensePlate = vehicleDto.LicensePlate,
                StartDate = DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(6).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = createdParkingLot!.Id
            };
            var reservationResult = await reservationController.CreateReservation(reservationDto);
            reservationResult.Should().BeOfType<CreatedAtActionResult>();
            var reservationResponse = ((CreatedAtActionResult)reservationResult).Value as ReservationResponseDto;
            reservationResponse.Should().NotBeNull();
            var reservationId = reservationResponse!.Id;

            // Start and stop session
            var startSessionDto = new StartSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            var stopSessionDto = new StopSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            await parkingLotController.StartSession(createdParkingLot.Id, startSessionDto);
            await parkingLotController.StopSession(createdParkingLot.Id, stopSessionDto);

            // Act: Delete reservation
            var reservationFromDb = dbContext.Reservations.Single(r => r.Id == reservationId);
            reservationFromDb.Should().NotBeNull();
            var vehicle = dbContext.Vehicles.Single(v => v.LicensePlate == vehicleDto.LicensePlate);
            var parkingLot = dbContext.ParkingLots.Single(p => p.Name == parkingLotDto.Name && p.Location == parkingLotDto.Location);
            var reservation = dbContext.Reservations.Single(r => r.VehicleId == vehicle.Id && r.ParkingLotId == parkingLot.Id);

            await archiveService.ArchiveReservationAsync(reservation, testUser.Role, testUser.Username);

            var archivedReservation = dbContext.ArchivedReservations.Single(ar => ar.VehicleId == vehicle.Id && ar.ParkingLotId == parkingLot.Id);

            //Assert
            Assert.Equal(archivedReservation.ParkingLotId, reservation.ParkingLotId);
            Assert.Equal(archivedReservation.VehicleId, reservation.VehicleId);
            Assert.Equal(archivedReservation.UserId, reservation.UserId);
            Assert.Equal(archivedReservation.Cost, reservation.Cost);
            Assert.Equal(archivedReservation.StartTime, reservation.StartTime);

        }


        [Fact]
        public async Task DeleteReservation_ShouldNotArchiveReservation_ForUser()
        {
            // Arrange
            var loggerMocks = new
            {
                ParkingLot = new Mock<ILogger<ParkingLotsController>>(),
                Reservation = new Mock<ILogger<ReservationController>>(),
                ReservationService = new Mock<ILogger<ReservationService>>(),
                ArchiveService = new Mock<ILogger<ArchiveService>>(),
                Vehicle = new Mock<ILogger<VehiclesController>>(),
                AutoComplete = new Mock<ILogger<ReservationAutoCompleteService>>(),
                PaymentService = new Mock<ILogger<PaymentGenerationService>>(),
                DiscountCode  = new Mock<ILogger<DiscountCodeService>>(),
                kadaster = new Mock<ILogger<KadasterAddressValidationService>>(),
                addressValidationMock = new Mock<IAddressValidationService>()
            };

            await using var dbContext = GetInMemoryDbContext("TestDb_ArchiveReservationsUser");
            dbContext.Database.EnsureDeleted();

            // Mock zodat elk adres geldig is tijdens tests
            loggerMocks.addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });
            // Services
            var discountService = new DiscountCodeService(dbContext, loggerMocks.DiscountCode.Object);
            var archiveService = new ArchiveService(dbContext, loggerMocks.ArchiveService.Object);
            var reservationService = new ReservationService(dbContext, loggerMocks.ReservationService.Object, archiveService, discountService);
            var paymentService = new PaymentGenerationService(dbContext, loggerMocks.PaymentService.Object);
            var autoCompleteService = new ReservationAutoCompleteService(dbContext, paymentService, loggerMocks.AutoComplete.Object);

            // Test user
            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "User"
            };

            var testAdmin = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };
            dbContext.Users.Add(testUser);
            dbContext.Users.Add(testAdmin);
            await dbContext.SaveChangesAsync();
            var userId = testUser.Id;
            var adminId = testAdmin.Id;

            // Controllers
            var parkingLotController = new ParkingLotsController(dbContext, loggerMocks.ParkingLot.Object, loggerMocks.Reservation.Object, reservationService, loggerMocks.addressValidationMock.Object)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username)
            };
            var parkingLotControllerAdmin = new ParkingLotsController(dbContext, loggerMocks.ParkingLot.Object, loggerMocks.Reservation.Object, reservationService, loggerMocks.addressValidationMock.Object)
            {
                ControllerContext = GetControllerContext(adminId, testAdmin.Username, testAdmin.Role)
            };
            var vehicleController = new VehiclesController(dbContext, loggerMocks.Vehicle.Object, archiveService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username)
            };

            var reservationController = new ReservationController(reservationService, dbContext, loggerMocks.Reservation.Object, autoCompleteService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username)
            };

            // Create vehicle
            var vehicleDto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-P",
                Make = "TestMakeS",
                Model = "TestModelS",
                Color = "GradientBlue",
                Year = 1999
            };
            var vehicleResult = await vehicleController.CreateVehicle(vehicleDto);
            vehicleResult.Should().BeOfType<OkObjectResult>();
            var createdVehicle = ((OkObjectResult)vehicleResult).Value as Vehicles;
            createdVehicle.Should().NotBeNull();

            // Create parking lot
            var parkingLotDto = new CreateParkingLotRequestDto
            {
                Name = "testtest",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 101,
                Reserved = 11,
                Tariff = 3.5m,
                DayTariff = 21.0m,
                Location = "Testtest Location",
                Lat = 53.5,
                Lng = 5.89
            };
            var parkingLotResult = await parkingLotControllerAdmin.CreateParkingLot(parkingLotDto);
            parkingLotResult.Should().BeOfType<CreatedAtActionResult>();
            var createdParkingLot = ((CreatedAtActionResult)parkingLotResult).Value
                .GetType().GetProperty("parkingLot")?.GetValue(((CreatedAtActionResult)parkingLotResult).Value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            // Create reservation
            var reservationDto = new ReservationDto
            {
                LicensePlate = vehicleDto.LicensePlate,
                StartDate = DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(6).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = createdParkingLot!.Id
            };
            var reservationResult = await reservationController.CreateReservation(reservationDto);
            reservationResult.Should().BeOfType<CreatedAtActionResult>();
            var reservationResponse = ((CreatedAtActionResult)reservationResult).Value as ReservationResponseDto;
            reservationResponse.Should().NotBeNull();
            var reservationId = reservationResponse!.Id;

            // Start and stop session
            var startSessionDto = new StartSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            var stopSessionDto = new StopSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            await parkingLotController.StartSession(createdParkingLot.Id, startSessionDto);
            await parkingLotController.StopSession(createdParkingLot.Id, stopSessionDto);

            // Act: Delete reservation
            var reservationFromDb = dbContext.Reservations.Single(r => r.Id == reservationId);
            reservationFromDb.Should().NotBeNull();
            var vehicle = dbContext.Vehicles.Single(v => v.LicensePlate == vehicleDto.LicensePlate);
            var parkingLot = dbContext.ParkingLots.Single(p => p.Name == parkingLotDto.Name && p.Location == parkingLotDto.Location);
            var reservation = dbContext.Reservations.Single(r => r.VehicleId == vehicle.Id && r.ParkingLotId == parkingLot.Id);

            //Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await archiveService.ArchiveReservationAsync(reservation, testUser.Role, testUser.Username)
            );
            var archivedReservation = dbContext.ArchivedReservations.SingleOrDefault(ar => ar.VehicleId == vehicle.Id && ar.ParkingLotId == parkingLot.Id);

            Assert.Equal(archivedReservation, null);

        }

        [Fact]
        public async Task DeletePayment_ShouldArchivePayment_ForAdmin()
        {
            // Arrange
            var loggerMocks = new
            {
                ParkingLot = new Mock<ILogger<ParkingLotsController>>(),
                Reservation = new Mock<ILogger<ReservationController>>(),
                ReservationService = new Mock<ILogger<ReservationService>>(),
                ArchiveService = new Mock<ILogger<ArchiveService>>(),
                Vehicle = new Mock<ILogger<VehiclesController>>(),
                AutoComplete = new Mock<ILogger<ReservationAutoCompleteService>>(),
                PaymentService = new Mock<ILogger<PaymentGenerationService>>(),
                DiscountCode  = new Mock<ILogger<DiscountCodeService>>(),
                kadaster = new Mock<ILogger<KadasterAddressValidationService>>(),
                addressValidationMock = new Mock<IAddressValidationService>()
            };

            await using var dbContext = GetInMemoryDbContext("TestDb_ArchivePayments");
            dbContext.Database.EnsureDeleted();

            // Mock zodat elk adres geldig is tijdens tests
            loggerMocks.addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });
            // Services
            var discountService = new DiscountCodeService(dbContext, loggerMocks.DiscountCode.Object);
            var archiveService = new ArchiveService(dbContext, loggerMocks.ArchiveService.Object);
            var reservationService = new ReservationService(dbContext, loggerMocks.ReservationService.Object, archiveService, discountService);
            var paymentService = new PaymentGenerationService(dbContext, loggerMocks.PaymentService.Object);
            var autoCompleteService = new ReservationAutoCompleteService(dbContext, paymentService, loggerMocks.AutoComplete.Object);

            // Test user
            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };
            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();
            var userId = testUser.Id;

            var paymentServiceMock = new PaymentService(
                dbContext,
                discountService,
                archiveService
            );
            // Controllers
            var parkingLotController = new ParkingLotsController(dbContext, loggerMocks.ParkingLot.Object, loggerMocks.Reservation.Object, reservationService, loggerMocks.addressValidationMock.Object)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var vehicleController = new VehiclesController(dbContext, loggerMocks.Vehicle.Object, archiveService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var reservationController = new ReservationController(reservationService, dbContext, loggerMocks.Reservation.Object, autoCompleteService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var paymentController = new PaymentsController(paymentServiceMock, dbContext, paymentService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            // Create vehicle
            var vehicleDto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-P",
                Make = "TestMakeS",
                Model = "TestModelS",
                Color = "GradientBlue",
                Year = 1999
            };
            var vehicleResult = await vehicleController.CreateVehicle(vehicleDto);
            vehicleResult.Should().BeOfType<OkObjectResult>();
            var createdVehicle = ((OkObjectResult)vehicleResult).Value as Vehicles;
            createdVehicle.Should().NotBeNull();

            // Create parking lot
            var parkingLotDto = new CreateParkingLotRequestDto
            {
                Name = "testtest",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 101,
                Reserved = 11,
                Tariff = 3.5m,
                DayTariff = 21.0m,
                Location = "Testtest Location",
                Lat = 53.5,
                Lng = 5.89
            };
            var parkingLotResult = await parkingLotController.CreateParkingLot(parkingLotDto);
            parkingLotResult.Should().BeOfType<CreatedAtActionResult>();
            var createdParkingLot = ((CreatedAtActionResult)parkingLotResult).Value
                .GetType().GetProperty("parkingLot")?.GetValue(((CreatedAtActionResult)parkingLotResult).Value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            // Create reservation
            var reservationDto = new ReservationDto
            {
                LicensePlate = vehicleDto.LicensePlate,
                StartDate = DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(6).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = createdParkingLot!.Id
            };
            var reservationResult = await reservationController.CreateReservation(reservationDto);
            reservationResult.Should().BeOfType<CreatedAtActionResult>();
            var reservationResponse = ((CreatedAtActionResult)reservationResult).Value as ReservationResponseDto;
            reservationResponse.Should().NotBeNull();
            var reservationId = reservationResponse!.Id;

            // Start and stop session
            var startSessionDto = new StartSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            var stopSessionDto = new StopSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            await parkingLotController.StartSession(createdParkingLot.Id, startSessionDto);
            await parkingLotController.StopSession(createdParkingLot.Id, stopSessionDto);

            // Act: Delete reservation
            var reservationFromDb = dbContext.Reservations.Single(r => r.Id == reservationId);
            reservationFromDb.Should().NotBeNull();
            var vehicle = dbContext.Vehicles.Single(v => v.LicensePlate == vehicleDto.LicensePlate);
            var reservation = dbContext.Reservations.Single(r => r.VehicleId == vehicle.Id && r.ParkingLotId == createdParkingLot.Id);
            //create payment
            var paymentDto = new CreatedPaymentDto
            {
                ParkingLotId = createdParkingLot.Id,
                LicensePlate = vehicle.LicensePlate,
                Duration = 1200
            };
            var createPayment = await paymentController.CreatePayment(paymentDto);
            var payment = dbContext.Payments.Single(p => p.ParkingLotId == createdParkingLot.Id && p.LicensePlate == vehicle.LicensePlate);

            
            await archiveService.ArchiveAndDeletePaymentAsync(payment, testUser.Role, testUser.Id);

            var archivedPayment = dbContext.ArchivedPayments.Single(ap => ap.ParkingLotId == createdParkingLot.Id && ap.LicensePlate == vehicle.LicensePlate);

            //Assert
            Assert.Equal(archivedPayment.ParkingLotId, payment.ParkingLotId);
            Assert.Equal(archivedPayment.UserId, payment.UserId);
            Assert.Equal(archivedPayment.LicensePlate, payment.LicensePlate);
            Assert.Equal(archivedPayment.Duration, payment.Duration);
            Assert.Equal(archivedPayment.PaymentStatus, payment.PaymentStatus);
            Assert.Equal(archivedPayment.StartTime, payment.StartTime);
            Assert.Equal(archivedPayment.EndTime, payment.EndTime);
        }

        [Fact]
        public async Task DeletePayment_ShouldNotArchivePayment_ForUser()
        {
            // Arrange
            var loggerMocks = new
            {
                ParkingLot = new Mock<ILogger<ParkingLotsController>>(),
                Reservation = new Mock<ILogger<ReservationController>>(),
                ReservationService = new Mock<ILogger<ReservationService>>(),
                ArchiveService = new Mock<ILogger<ArchiveService>>(),
                Vehicle = new Mock<ILogger<VehiclesController>>(),
                AutoComplete = new Mock<ILogger<ReservationAutoCompleteService>>(),
                PaymentService = new Mock<ILogger<PaymentGenerationService>>(),
                DiscountCode  = new Mock<ILogger<DiscountCodeService>>(),
                kadaster = new Mock<ILogger<KadasterAddressValidationService>>(),
                addressValidationMock = new Mock<IAddressValidationService>()
            };

            await using var dbContext = GetInMemoryDbContext("TestDb_ArchivePayments2");
            dbContext.Database.EnsureDeleted();

            // Mock zodat elk adres geldig is tijdens tests
            loggerMocks.addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });
            // Services
            var discountService = new DiscountCodeService(dbContext, loggerMocks.DiscountCode.Object);
            var archiveService = new ArchiveService(dbContext, loggerMocks.ArchiveService.Object);
            var reservationService = new ReservationService(dbContext, loggerMocks.ReservationService.Object, archiveService, discountService);
            var paymentService = new PaymentGenerationService(dbContext, loggerMocks.PaymentService.Object);
            var autoCompleteService = new ReservationAutoCompleteService(dbContext, paymentService, loggerMocks.AutoComplete.Object);

            // Test user
            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "User"
            };

            var testAdmin = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };
            dbContext.Users.Add(testUser);
            dbContext.Users.Add(testAdmin);
            await dbContext.SaveChangesAsync();
            var userId = testUser.Id;

            var paymentServiceMock = new PaymentService(
                dbContext,
                discountService,
                archiveService
            );
            // Controllers
            var parkingLotController = new ParkingLotsController(dbContext, loggerMocks.ParkingLot.Object, loggerMocks.Reservation.Object, reservationService, loggerMocks.addressValidationMock.Object)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testAdmin.Role)
            };

            var vehicleController = new VehiclesController(dbContext, loggerMocks.Vehicle.Object, archiveService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            var reservationController = new ReservationController(reservationService, dbContext, loggerMocks.Reservation.Object, autoCompleteService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testAdmin.Role)
            };

            var paymentControllerLoggedIn = new PaymentsController(paymentServiceMock, dbContext, paymentService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testAdmin.Role)
            };

            var paymentController = new PaymentsController(paymentServiceMock, dbContext, paymentService)
            {
                ControllerContext = GetControllerContext(userId, testUser.Username, testUser.Role)
            };

            // Create vehicle
            var vehicleDto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-P",
                Make = "TestMakeS",
                Model = "TestModelS",
                Color = "GradientBlue",
                Year = 1999
            };
            var vehicleResult = await vehicleController.CreateVehicle(vehicleDto);
            vehicleResult.Should().BeOfType<OkObjectResult>();
            var createdVehicle = ((OkObjectResult)vehicleResult).Value as Vehicles;
            createdVehicle.Should().NotBeNull();

            // Create parking lot
            var parkingLotDto = new CreateParkingLotRequestDto
            {
                Name = "testtest",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 101,
                Reserved = 11,
                Tariff = 3.5m,
                DayTariff = 21.0m,
                Location = "Testtest Location",
                Lat = 53.5,
                Lng = 5.89
            };
            var parkingLotResult = await parkingLotController.CreateParkingLot(parkingLotDto);
            parkingLotResult.Should().BeOfType<CreatedAtActionResult>();
            var createdParkingLot = ((CreatedAtActionResult)parkingLotResult).Value
                .GetType().GetProperty("parkingLot")?.GetValue(((CreatedAtActionResult)parkingLotResult).Value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            // Create reservation
            var reservationDto = new ReservationDto
            {
                LicensePlate = vehicleDto.LicensePlate,
                StartDate = DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(6).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = createdParkingLot!.Id
            };
            var reservationResult = await reservationController.CreateReservation(reservationDto);
            reservationResult.Should().BeOfType<CreatedAtActionResult>();
            var reservationResponse = ((CreatedAtActionResult)reservationResult).Value as ReservationResponseDto;
            reservationResponse.Should().NotBeNull();
            var reservationId = reservationResponse!.Id;

            // Start and stop session
            var startSessionDto = new StartSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            var stopSessionDto = new StopSessionRequestDto { LicensePlate = vehicleDto.LicensePlate };
            await parkingLotController.StartSession(createdParkingLot.Id, startSessionDto);
            await parkingLotController.StopSession(createdParkingLot.Id, stopSessionDto);

            // Act: Delete reservation
            var reservationFromDb = dbContext.Reservations.Single(r => r.Id == reservationId);
            reservationFromDb.Should().NotBeNull();
            var vehicle = dbContext.Vehicles.Single(v => v.LicensePlate == vehicleDto.LicensePlate);
            var reservation = dbContext.Reservations.Single(r => r.VehicleId == vehicle.Id && r.ParkingLotId == createdParkingLot.Id);
            //create payment
            var paymentDto = new CreatedPaymentDto
            {
                ParkingLotId = createdParkingLot.Id,
                LicensePlate = vehicle.LicensePlate,
                Duration = 1200
            };
            var createPayment = await paymentControllerLoggedIn.CreatePayment(paymentDto);
            var payment = dbContext.Payments.Single(p => p.ParkingLotId == createdParkingLot.Id && p.LicensePlate == vehicle.LicensePlate);


            //Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await archiveService.ArchiveAndDeletePaymentAsync(payment, testUser.Role, testUser.Id)
            );
            var archivedPayment = dbContext.ArchivedPayments.SingleOrDefault(ap => ap.ParkingLotId == createdParkingLot.Id && ap.LicensePlate == vehicle.LicensePlate);
            Assert.Equal(archivedPayment, null);
        }
    }
}
