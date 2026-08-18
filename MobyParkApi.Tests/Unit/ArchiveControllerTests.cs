using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MobyParkApi.Models;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using MobyParkApi.Models.Dto;
using MobyParkApi.Service;
using System.Security.Cryptography.X509Certificates;

namespace MobyParkApi.Tests.Unit
{
    public class ArchiveControllerTests
    {
        [Fact]
        public async Task GetArchivedInvoices_ReturnsUnauthorized_ForUnAuthenticatedUser()
        {
            // Arrange
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            // In-memory EF context
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_Unauth")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            // Unauthenticated user
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // no authentication
                }
            };

            // Act
            var result = await controller.GetArchivedInvoices();

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            invoiceServiceMock.Verify(s => s.GetArchivedInvoicesForUserAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetArchivedInvoices_ReturnsOk_ForAuthenticatedUser()
        {
            // Arrange
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var archivedInvoices = new List<ArchivedInvoices>
            {
                new ArchivedInvoices { Id = 1, UserId = 1, TotalAmount = 100 },
                new ArchivedInvoices { Id = 2, UserId = 1, TotalAmount = 200 }
            };

            invoiceServiceMock
                .Setup(s => s.GetArchivedInvoicesForUserAsync(1))
                .ReturnsAsync(archivedInvoices);

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_Auth")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            // Authenticated user
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "1"),
                            new Claim(ClaimTypes.Name, "testuser")
                        }, "TestAuth"))
                }
            };

            // Act
            var result = await controller.GetArchivedInvoices();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(archivedInvoices);

            invoiceServiceMock.Verify(s => s.GetArchivedInvoicesForUserAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetArchivedInvoice_ReturnsUnauthorized_ForUnAuthenticatedUser()
        {
            // Arrange
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            // In-memory EF context
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_Unauth")
                .Options;
            var archivedInvoices = new List<ArchivedInvoices>
            {
                new ArchivedInvoices { Id = 1, UserId = 5, TotalAmount = 100 },
                new ArchivedInvoices { Id = 2, UserId = 5, TotalAmount = 200 }
            };
            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            // Unauthenticated user
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // no authentication
                }
            };

            // Act
            var result = await controller.GetArchivedInvoice(archivedInvoices[0].Id);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            invoiceServiceMock.Verify(s => s.GetArchivedInvoicesForUserAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetArchivedInvoice_ReturnsNotFound_ForNonExistingInvoice()
        {
            // Arrange
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };

            var nonExistentInvoiceId = 3;

            // Mock service to return null for this ID
            invoiceServiceMock
                .Setup(s => s.GetArchivedInvoiceAsync(nonExistentInvoiceId))
                .ReturnsAsync((ArchivedInvoices?)null);

            // Act
            var result = await controller.GetArchivedInvoice(nonExistentInvoiceId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { error = "Archived invoice not found" });

            invoiceServiceMock.Verify(s => s.GetArchivedInvoiceAsync(nonExistentInvoiceId), Times.Once);
        }

        [Fact]
        public async Task GetArchivedInvoice_ReturnsForbid_ForInvoiceBelongingToAnotherUser()
        {
            // Arrange
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };

            // Mock invoice belongs to another user
            var invoiceForAnotherUser = new ArchivedInvoices
            {
                Id = 2,
                UserId = 99,  // Different from logged-in user
                TotalAmount = 200
            };

            invoiceServiceMock
                .Setup(s => s.GetArchivedInvoiceAsync(2))
                .ReturnsAsync(invoiceForAnotherUser);

            // Act
            var result = await controller.GetArchivedInvoice(2);

            // Assert
            result.Should().BeOfType<ForbidResult>();
            invoiceServiceMock.Verify(s => s.GetArchivedInvoiceAsync(2), Times.Once);
        }

        [Fact]
        public async Task ArchiveSpecificInvoice_ReturnsOk_ForAuthenticatedUser()
        {
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            var invoiceId = 1;
            invoiceServiceMock
                .Setup(s => s.ArchiveInvoiceAsync(invoiceId, "testuser"))
                .ReturnsAsync(true);

            // Act
            var result = await controller.ArchiveSpecificInvoice(invoiceId);



            // Assert
            result.Should().BeOfType<OkObjectResult>();

            invoiceServiceMock.Verify(s => s.ArchiveInvoiceAsync(invoiceId, "testuser"), Times.Once);
        }

        [Fact]
        public async Task ArchiveSpecificInvoice_ReturnsBadRequest_ForAuthenticatedUser()
        {
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            var invoiceId = 1;
            invoiceServiceMock
                .Setup(s => s.ArchiveInvoiceAsync(invoiceId, "testuser"))
                .ReturnsAsync(false);

            // Act
            var result = await controller.ArchiveSpecificInvoice(invoiceId);



            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();

            invoiceServiceMock.Verify(s => s.ArchiveInvoiceAsync(invoiceId, "testuser"), Times.Once);
        }

        [Fact]
        public async Task ArchiveAllPaidInvoices_ReturnsOkResult_ForAuthenticatedUser()
        {
            var invoiceServiceMock = new Mock<IInvoiceArchiveService>();
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ArchiveController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new ArchiveController(
                archiveServiceMock.Object,
                invoiceServiceMock.Object,
                dbContext,
                loggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            invoiceServiceMock
                .Setup(s => s.ArchiveAllPaidInvoicesAsync("testuser"))
                .ReturnsAsync(5);
                

            // Act
            var result = await controller.ArchiveAllPaidInvoices();



            // Assert
            result.Should().BeOfType<OkObjectResult>();

            invoiceServiceMock.Verify(s => s.ArchiveAllPaidInvoicesAsync("testuser"), Times.Once);
        }

        [Fact]
        public async Task DeleteVehicle_ReturnsOkResult_ForAuthenticatedUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<VehiclesController>>();
            var loggerArchiveServiceMock = new Mock<ILogger<ArchiveService>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedVehicle")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash"
            };
            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();
            
            var archiveService = new ArchiveService(
                dbContext,
                loggerArchiveServiceMock.Object
            );

            var controller = new VehiclesController(
                dbContext,
                loggerMock.Object,
                archiveServiceMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            var Vehicle = new Vehicles
            {
                Id = 1,
                UserId = 1,
                LicensePlate = "AB-123-A"
            };
            var Dto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-A",
                Make = "TestMake",
                Model = "TestModel",
                Color = "Red",
                Year = 2020
            };
            var assertResult = await controller.CreateVehicle(Dto);
            var vehicle = dbContext.Vehicles.Single(v => v.LicensePlate == Dto.LicensePlate);
            // Act
            var result = await controller.DeleteVehicle(vehicle.Id);
            await archiveService.ArchiveVehicleAndReservationsAsync(vehicle, testUser.Username);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            var archivedVehicle = dbContext.ArchivedVehicles.Single(av => av.LicensePlate == Vehicle.LicensePlate);
            Assert.Equal(archivedVehicle.UserId, vehicle.UserId);
            Assert.Equal(archivedVehicle.LicensePlate, vehicle.LicensePlate);
            Assert.Equal(archivedVehicle.Make, vehicle.Make);
            Assert.Equal(archivedVehicle.Model, vehicle.Model);
            Assert.Equal(archivedVehicle.Color, vehicle.Color);

        }

        [Fact]
        public async Task DeleteVehicle_ReturnsNotFound_ForAuthenticatedUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<VehiclesController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedVehicle")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new VehiclesController(
                dbContext,
                loggerMock.Object,
                archiveServiceMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            var Vehicle = new Vehicles
            {
                Id = 99,
                UserId = 1,
                LicensePlate = "AB-123-A"
            };
            
            // Act
            var result = await controller.DeleteVehicle(Vehicle.Id);



            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();

            archiveServiceMock.Verify(
            s => s.ArchiveVehicleAndReservationsAsync(
                It.Is<Vehicles>(v => v.Id == Vehicle.Id),
                "testuser"),
            Times.Never);
        }

        [Fact]
        public async Task DeleteVehicle_ReturnsUnauthorized_ForUnAuthenticatedUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<VehiclesController>>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedVehicle")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var controller = new VehiclesController(
                dbContext,
                loggerMock.Object,
                archiveServiceMock.Object);
            
            // Unauthenticated user
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // no authentication
                }
            };

            var Vehicle = new Vehicles
            {
                Id = 1,
                UserId = 1,
                LicensePlate = "AB-123-A"
            };
            
            var Dto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-A",
                Make = "TestMake",
                Model = "TestModel",
                Color = "Red",
                Year = 2020
            };
            var assertResult = await controller.CreateVehicle(Dto);

            // Act
            var result = await controller.DeleteVehicle(Vehicle.Id);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            archiveServiceMock.Verify(
            s => s.ArchiveVehicleAndReservationsAsync(
                It.Is<Vehicles>(v => v.Id == Vehicle.Id),
                "testuser"),
            Times.Never);
        }


        [Fact]
        public async Task DeleteParkingLot_ReturnsOkResult_ForAdmin()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ParkingLotsController>>();
            var loggerReservationMock = new Mock<ILogger<ReservationController>>();
            var loggerReservationServiceMock = new Mock<ILogger<ReservationService>>();
            var loggerArchiveServiceMock = new Mock<ILogger<ArchiveService>>();
            var DiscountCode  = new Mock<ILogger<DiscountCodeService>>();
            var addressValidationMock = new Mock<IAddressValidationService>();

            // <<<<<< NODIGE AANPASSING: mock return voor GetAddressAsync >>>>>>
            addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "3071 AC",
                    woonplaatsnaam = "Rotterdam"
                });

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedParkingLot4")
                .Options;
            
            using var dbContext = new ApplicationDbContext(dbOptions);
            
            var discountService = new DiscountCodeService(dbContext, DiscountCode.Object);

            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };

            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();

            var reservationService = new ReservationService(
                dbContext,
                loggerReservationServiceMock.Object,
                archiveServiceMock.Object,
                discountService);

            var parkingLotService = new ParkingLotService(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object
            );

            var controller = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser"),
                        new Claim(ClaimTypes.Role, testUser.Role)

                    }, "TestAuth"))
                }
            };

            var dto = new CreateParkingLotRequestDto
            {
                Name = "second",
                Postcode = "3071 AC",
                HouseNumber = 7,
                Capacity = 100,
                Reserved = 10,
                Tariff = 2.5m,
                DayTariff = 20.0m,
                Location = "Test Location",
                Lat = 52.5,
                Lng = 4.89
            };

            // Arrange – create parking lot
            var createResult = await controller.CreateParkingLot(dto);
            createResult.Should().BeOfType<CreatedAtActionResult>();

            var createdAtResult = (CreatedAtActionResult)createResult;
            var value = createdAtResult.Value!;

            var parkingLotProperty = value.GetType().GetProperty("parkingLot");
            parkingLotProperty.Should().NotBeNull();

            var createdParkingLot = parkingLotProperty!.GetValue(value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            var parkingLot = dbContext.ParkingLots.Single(p => p.Name == dto.Name && p.Location == dto.Location);

            // Act
            var result = await controller.DeleteParkingLot(parkingLot.Id);

            var archivedParkingLot = dbContext.ArchivedParkingLots.Single(ap => ap.Name == parkingLot.Name && ap.Address == parkingLot.Address && ap.Location == parkingLot.Location);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            Assert.Equal(archivedParkingLot.Name, parkingLot.Name);
            Assert.Equal(archivedParkingLot.Address, parkingLot.Address);
            Assert.Equal(archivedParkingLot.Location, parkingLot.Location);
        }


        [Fact]
        public async Task DeleteParkingLot_ReturnsUnAuthorized_ForUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ParkingLotsController>>();
            var loggerReservationMock = new Mock<ILogger<ReservationController>>();
            var loggerReservationServiceMock = new Mock<ILogger<ReservationService>>();
            var loggerArchiveServiceMock = new Mock<ILogger<ArchiveService>>();
            var DiscountCode  = new Mock<ILogger<DiscountCodeService>>();
            var addressValidationMock = new Mock<IAddressValidationService>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedParkingLotUnauthorized")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);
            dbContext.Database.EnsureDeleted();
            var discountService = new DiscountCodeService(dbContext, DiscountCode.Object);
            
             // <<<<<< NODIGE AANPASSING: mock return voor GetAddressAsync >>>>>>
            addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "3071 AD",
                    woonplaatsnaam = "Rotterdam"
                });

            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "user"
            };

            var testUserAdmin = new Users
            {
                Username = "testuseradmin",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };

            dbContext.Users.Add(testUser);
            dbContext.Users.Add(testUserAdmin);
            await dbContext.SaveChangesAsync();

            var reservationService = new ReservationService(
                dbContext,
                loggerReservationServiceMock.Object,
                archiveServiceMock.Object,
                discountService);

            var parkingLotService = new ParkingLotService(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object
            );

            var controller = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);

            var controllerAdmin = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser"),
                        new Claim(ClaimTypes.Role, testUser.Role)

                    }, "TestAuth"))
                }
            };
            
            controllerAdmin.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser"),
                        new Claim(ClaimTypes.Role, testUserAdmin.Role)

                    }, "TestAuth"))
                }
            };

            var dto = new CreateParkingLotRequestDto
            {
                Name = "second",
                Postcode = "3071 AD",
                HouseNumber = 7,
                Capacity = 100,
                Reserved = 10,
                Tariff = 2.5m,
                DayTariff = 20.0m,
                Location = "Test Location",
                Lat = 52.5,
                Lng = 4.89
            };

            // Arrange – create parking lot
            var createResult = await controllerAdmin.CreateParkingLot(dto);
            createResult.Should().BeOfType<CreatedAtActionResult>();

            var createdAtResult = (CreatedAtActionResult)createResult;
            var value = createdAtResult.Value!;

            var parkingLotProperty = value.GetType().GetProperty("parkingLot");
            parkingLotProperty.Should().NotBeNull();

            var createdParkingLot = parkingLotProperty!.GetValue(value) as ParkingLots;
            createdParkingLot.Should().NotBeNull();

            var parkingLot = dbContext.ParkingLots.Single(p => p.Name == dto.Name && p.Location == dto.Location);

            // Act
            //await parkingLotService.DeleteParkingLotService(parkingLot.Id, controller.HttpContext.User);
            var result = await controller.DeleteParkingLot(parkingLot.Id);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task StopSession_ReturnOk_ForUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ParkingLotsController>>();
            var loggerVehicleMock = new Mock<ILogger<VehiclesController>>();
            var loggerReservationMock = new Mock<ILogger<ReservationController>>();
            var loggerReservationServiceMock = new Mock<ILogger<ReservationService>>();
            var DiscountCode  = new Mock<ILogger<DiscountCodeService>>();
            var addressValidationMock = new Mock<IAddressValidationService>();
            // Mock zodat elk adres geldig is tijdens tests
            addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedInvoice")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var discountService = new DiscountCodeService(dbContext, DiscountCode.Object);

            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };

            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();

            var reservationService = new ReservationService(
                dbContext,
                loggerReservationServiceMock.Object,
                archiveServiceMock.Object,
                discountService);

            var controller = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);
            
            var controllerAdmin = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);

            var vehicleController = new VehiclesController(
                dbContext,
                loggerVehicleMock.Object,
                archiveServiceMock.Object);
            vehicleController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };

            controllerAdmin.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser"),
                        new Claim(ClaimTypes.Role, testUser.Role)
                    }, "TestAuth"))
                }
            };

            var dto = new CreateParkingLotRequestDto
            {
                Name = "Test Parking Lot",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 100,
                Reserved = 10,
                Tariff = 2.5m,
                DayTariff = 20.0m,
                Location = "Test Location",
                Lat = 52.5,
                Lng = 4.89
            };
            var Dto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-A",
                Make = "TestMake",
                Model = "TestModel",
                Color = "Red",
                Year = 2020
            };
            var vehicleResult = await vehicleController.CreateVehicle(Dto);
            var okResult = Assert.IsType<OkObjectResult>(vehicleResult);
            var vehicle = Assert.IsType<Vehicles>(okResult.Value);
            var licensePlate = Convert.ToString(vehicle.LicensePlate);
            var startSessionRequestDto = new StartSessionRequestDto
            {
                LicensePlate = licensePlate
            };
            var stopSessionRequestDto = new StopSessionRequestDto
            {
                LicensePlate = licensePlate
            };

            // Arrange – create parking lot 
            var createResult = await controllerAdmin.CreateParkingLot(dto);
            var createdParkingLot = Assert.IsType<CreatedAtActionResult>(createResult);

            var parkingLotProperty = createdParkingLot.Value
                .GetType()
                .GetProperty("parkingLot");

            var parkingLot = Assert.IsType<ParkingLots>(
                parkingLotProperty.GetValue(createdParkingLot.Value)
            );
            
            // Act
            var result = await controller.StartSession(parkingLot.Id, startSessionRequestDto);
            var okResultstartParkingSession = Assert.IsType<OkObjectResult>(result);
            var startParkingSession = Assert.IsType<string>(okResultstartParkingSession.Value);
            var startedSession = dbContext.ParkingSessions
                .Single(s => s.Id == parkingLot.Id);
            var stopSession = await controller.StopSession(parkingLot.Id, stopSessionRequestDto);
            var okResultStopParkingSession = Assert.IsType<OkObjectResult>(stopSession);
            var ArchivedSession = dbContext.ArchivedParkingSessions
                .Single(s => s.Id == startedSession.Id);

            // Assert
            stopSession.Should().BeOfType<OkObjectResult>();
            Assert.NotNull(ArchivedSession.Stopped);
            Assert.Equal(startedSession.LicensePlate, ArchivedSession.LicensePlate);
        }

        [Fact]
        public async Task StopSession_ReturnNotFound_ForUser()
        {
            var archiveServiceMock = new Mock<IArchiveService>();
            var loggerMock = new Mock<ILogger<ParkingLotsController>>();
            var loggerVehicleMock = new Mock<ILogger<VehiclesController>>();
            var loggerReservationMock = new Mock<ILogger<ReservationController>>();
            var loggerReservationServiceMock = new Mock<ILogger<ReservationService>>();
            var DiscountCode  = new Mock<ILogger<DiscountCodeService>>();
            var addressValidationMock = new Mock<IAddressValidationService>();

            // <<<<<< NODIGE AANPASSING: mock return voor GetAddressAsync >>>>>>
            addressValidationMock
                .Setup(a => a.GetAddressAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new PdokDocAddressResponseDto
                {
                    straatnaam = "Test Street",
                    huisnummer = 7,
                    postcode = "1012 WX",
                    woonplaatsnaam = "Rotterdam"
                });

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TestDb_ArchivedNotFound")
                .Options;

            using var dbContext = new ApplicationDbContext(dbOptions);

            var discountService = new DiscountCodeService(dbContext, DiscountCode.Object);

            var testUser = new Users
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "fakehash",
                Role = "Admin"
            };

            dbContext.Users.Add(testUser);
            await dbContext.SaveChangesAsync();

            var reservationService = new ReservationService(
                dbContext,
                loggerReservationServiceMock.Object,
                archiveServiceMock.Object,
                discountService);

            var controller = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);
            
            var controllerAdmin = new ParkingLotsController(
                dbContext,
                loggerMock.Object,
                loggerReservationMock.Object,
                reservationService,
                addressValidationMock.Object);

            var vehicleController = new VehiclesController(
                dbContext,
                loggerVehicleMock.Object,
                archiveServiceMock.Object);
            vehicleController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser")
                    }, "TestAuth"))
                }
            };

            controllerAdmin.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Name, "testuser"),
                        new Claim(ClaimTypes.Role, testUser.Role)
                    }, "TestAuth"))
                }
            };

            var dto = new CreateParkingLotRequestDto
            {
                Name = "Test Parking Lot",
                Postcode = "1012 WX",
                HouseNumber = 7,
                Capacity = 100,
                Reserved = 10,
                Tariff = 2.5m,
                DayTariff = 20.0m,
                Location = "Test Location",
                Lat = 52.5,
                Lng = 4.89
            };
            var Dto = new CreateVehicleRequestDto
            {
                LicensePlate = "AB-123-A",
                Make = "TestMake",
                Model = "TestModel",
                Color = "Red",
                Year = 2020
            };
            var vehicleResult = await vehicleController.CreateVehicle(Dto);
            var okResult = Assert.IsType<OkObjectResult>(vehicleResult);
            var vehicle = Assert.IsType<Vehicles>(okResult.Value);
            var licensePlate = Convert.ToString(vehicle.LicensePlate);
            var startSessionRequestDto = new StartSessionRequestDto
            {
                LicensePlate = licensePlate
            };
            var stopSessionRequestDto = new StopSessionRequestDto
            {
                LicensePlate = licensePlate
            };

            // Arrange – create parking lot 
            var createResult = await controllerAdmin.CreateParkingLot(dto);
            var createdParkingLot = Assert.IsType<CreatedAtActionResult>(createResult);

            var parkingLotProperty = createdParkingLot.Value
                .GetType()
                .GetProperty("parkingLot");

            var parkingLot = Assert.IsType<ParkingLots>(
                parkingLotProperty.GetValue(createdParkingLot.Value)
            );
            
            // Act
            var result = await controller.StartSession(parkingLot.Id, startSessionRequestDto);
            var okResultstartParkingSession = Assert.IsType<OkObjectResult>(result);
            var startParkingSession = Assert.IsType<string>(okResultstartParkingSession.Value);
            var startedSession = dbContext.ParkingSessions
                .Single(s => s.Id == parkingLot.Id);
            var stopSession = await controller.StopSession(6, stopSessionRequestDto);
            var okResultStopParkingSession = Assert.IsType<NotFoundObjectResult>(stopSession);
            var ArchivedSession = dbContext.ArchivedParkingSessions
                .SingleOrDefault(s => s.Id == startedSession.Id);

            // Assert
            stopSession.Should().BeOfType<NotFoundObjectResult>();
            Assert.Equal(ArchivedSession, null);
        }
    }
}