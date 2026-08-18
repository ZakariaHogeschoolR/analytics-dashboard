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

namespace MobyParkApi.Tests.E2E
{
    public class ReservationFlowE2ETests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ApplicationDbContext _context;

        public ReservationFlowE2ETests(WebApplicationFactory<Program> factory)
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
                        options.UseInMemoryDatabase("E2ETestDB_" + Guid.NewGuid().ToString());
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
                new Users { Id = 2, Username = "user2", Email = "user2@test.com", Password = "hash", Role = "User", FirstName = "User", LastName = "Two", Created_At = DateTime.UtcNow },
                new Users { Id = 3, Username = "admin", Email = "admin@test.com", Password = "hash", Role = "Admin", FirstName = "Admin", LastName = "User", Created_At = DateTime.UtcNow }
            };
            _context.Users.AddRange(users);

            var parkingLots = new[]
            {
                new ParkingLots { Id = 1, Name = "Central", Location = "City", Address = "Main 1", Capacity = 10, Reserved = 0, Tariff = 2.50m, DayTariff = 20.00m, Coordinates = "52.370216,4.895168" },
                new ParkingLots { Id = 2, Name = "Airport", Location = "Airport", Address = "Airport 1", Capacity = 50, Reserved = 0, Tariff = 5.00m, DayTariff = 40.00m, Coordinates = "52.308056,4.764167" }
            };
            _context.ParkingLots.AddRange(parkingLots);

            var vehicles = new[]
            {
                new Vehicles { Id = 1, UserId = 1, LicensePlate = "AA-111-BB", Brand = "Tesla", Model = "Model 3", CreatedAt = DateTime.UtcNow },
                new Vehicles { Id = 2, UserId = 1, LicensePlate = "CC-222-DD", Brand = "BMW", Model = "X5", CreatedAt = DateTime.UtcNow },
                new Vehicles { Id = 3, UserId = 2, LicensePlate = "EE-333-FF", Brand = "Audi", Model = "A4", CreatedAt = DateTime.UtcNow }
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
        public async Task CompleteUserJourney_CreateCheckUpdateCancel_Success()
        {
            // STEP 1: Create reservation
            SetAuthHeader(1, "User");
            var createRequest = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };

            var createResponse = await _client.PostAsJsonAsync("/api/reservation", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var reservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponseDto>();
            reservation.Should().NotBeNull();
            var reservationId = reservation!.Id;

            // STEP 2: Check reservation
            var getResponse = await _client.GetAsync($"/api/reservation/{reservationId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // STEP 3: Update reservation
            var updateRequest = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                StartDate = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss")
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/reservation/{reservationId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // STEP 4: Cancel reservation
            var deleteResponse = await _client.DeleteAsync($"/api/reservation/{reservationId}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task MultipleReservations_SameUserDifferentTimes_Success()
        {
            SetAuthHeader(1, "User");

            // Morning reservation
            var morning = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };
            var morningResponse = await _client.PostAsJsonAsync("/api/reservation", morning);
            morningResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Afternoon reservation
            var afternoon = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "CC-222-DD",
                StartDate = DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(6).ToString("yyyy-MM-dd HH:mm:ss")
            };
            var afternoonResponse = await _client.PostAsJsonAsync("/api/reservation", afternoon);
            afternoonResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Check all reservations
            var allResponse = await _client.GetAsync("/api/reservation");
            var reservations = await allResponse.Content.ReadFromJsonAsync<List<ReservationResponseDto>>();
            reservations.Should().HaveCount(2);
        }

        [Fact]
        public async Task DataIsolation_MultipleUsers_Success()
        {
            // User 1 creates reservation
            SetAuthHeader(1, "User");
            var user1Request = new ReservationDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };
            var user1Response = await _client.PostAsJsonAsync("/api/reservation", user1Request);
            var user1Reservation = await user1Response.Content.ReadFromJsonAsync<ReservationResponseDto>();

            // User 2 creates reservation
            SetAuthHeader(2, "User");
            var user2Request = new ReservationDto
            {
                ParkingLotId = 2,
                LicensePlate = "EE-333-FF",
                StartDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss")
            };
            var user2Response = await _client.PostAsJsonAsync("/api/reservation", user2Request);

            // User 1 sees only own
            SetAuthHeader(1, "User");
            var user1List = await _client.GetAsync("/api/reservation");
            var user1Reservations = await user1List.Content.ReadFromJsonAsync<List<ReservationResponseDto>>();
            user1Reservations.Should().HaveCount(1);

            // User 2 sees only own
            SetAuthHeader(2, "User");
            var user2List = await _client.GetAsync("/api/reservation");
            var user2Reservations = await user2List.Content.ReadFromJsonAsync<List<ReservationResponseDto>>();
            user2Reservations.Should().HaveCount(1);
        }
    }
}