using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.Integration
{
    public class ReservationsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ApplicationDbContext _context;

        public ReservationsIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("IntTestDB_" + Guid.NewGuid().ToString());
                    });
                });
            });

            _client = _factory.CreateClient();
            var scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            SeedTestData();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _client.Dispose();
        }

        private void SeedTestData()
        {
            var users = new[]
            {
                new Users { Id = 1, Username = "user1", Email = "user1@test.com", Password = "hash", Role = "User", FirstName = "User", LastName = "One", Created_At = DateTime.UtcNow },
                new Users { Id = 2, Username = "admin", Email = "admin@test.com", Password = "hash", Role = "Admin", FirstName = "Admin", LastName = "User", Created_At = DateTime.UtcNow }
            };
            _context.Users.AddRange(users);

            var parkingLots = new[]
            {
                new ParkingLots { Id = 1, Name = "Central Parking", Location = "Center", Address = "Main 1", Capacity = 10, Reserved = 0, Tariff = 2.50m, DayTariff = 20.00m, Coordinates = "52.370216,4.895168" }
            };
            _context.ParkingLots.AddRange(parkingLots);

            var vehicles = new[]
            {
                new Vehicles { Id = 1, UserId = 1, LicensePlate = "AB-123-CD", Brand = "Tesla", Model = "Model 3", CreatedAt = DateTime.UtcNow },
                new Vehicles { Id = 2, UserId = 2, LicensePlate = "XY-999-ZZ", Brand = "BMW", Model = "X5", CreatedAt = DateTime.UtcNow }
            };
            _context.Vehicles.AddRange(vehicles);

            _context.SaveChanges();
        }

        private void SetAuthHeader(int userId, string role)
        {
            var token = $"Bearer mock_token_user{userId}_role{role}";
            _client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }

        [Fact]
        public async Task POST_Reservations_ValidData_Returns201()
        {
            SetAuthHeader(1, "User");
            var request = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };

            var response = await _client.PostAsJsonAsync("/api/reservation", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var reservation = await response.Content.ReadFromJsonAsync<ReservationResponseDto>();
            reservation.Should().NotBeNull();
            reservation!.Status.Should().Be("Pending");
        }

        [Fact]
        public async Task POST_Reservations_InvalidParkingLot_Returns404()
        {
            SetAuthHeader(1, "User");
            var request = new ReservationDto
            {
                ParkingLotId = 999,
                LicensePlate = "AB-123-CD",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };

            var response = await _client.PostAsJsonAsync("/api/reservation", request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task POST_Reservations_NoAuth_Returns401()
        {
            var request = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };

            var response = await _client.PostAsJsonAsync("/api/reservation", request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GET_Reservations_AsUser_ReturnsOwnReservations()
        {
            SetAuthHeader(1, "User");
            _context.Reservations.Add(new Reservations
            {
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(3),
                Status = "Pending",
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var response = await _client.GetAsync("/api/reservation");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var reservations = await response.Content.ReadFromJsonAsync<List<ReservationResponseDto>>();
            reservations.Should().NotBeNull();
            reservations!.Should().HaveCount(1);
        }

        [Fact]
        public async Task GET_ReservationById_Exists_Returns200()
        {
            SetAuthHeader(1, "User");
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(3),
                Status = "Pending",
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var response = await _client.GetAsync("/api/reservation/1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GET_ReservationById_NotFound_Returns404()
        {
            SetAuthHeader(1, "User");

            var response = await _client.GetAsync("/api/reservation/999");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task PUT_Reservation_ValidData_Returns200()
        {
            SetAuthHeader(1, "User");
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(3),
                Status = "Pending",
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var updateRequest = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                StartDate = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };

            var response = await _client.PutAsJsonAsync("/api/reservation/1", updateRequest);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DELETE_Reservation_ValidRequest_Returns200()
        {
            SetAuthHeader(1, "User");
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(3),
                Status = "Pending",
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var response = await _client.DeleteAsync("/api/reservation/1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DELETE_Reservation_AlreadyStarted_Returns400()
        {
            SetAuthHeader(1, "User");
            var reservation = new Reservations
            {
                Id = 1,
                UserId = 1,
                ParkingLotId = 1,
                VehicleId = 1,
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow.AddHours(1),
                Status = "Pending",
                Cost = 5.00m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var response = await _client.DeleteAsync("/api/reservation/1");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task POST_CompleteExpired_AsAdmin_Returns200()
        {
            SetAuthHeader(2, "Admin");

            var response = await _client.PostAsync("/api/reservation/complete-expired", null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}