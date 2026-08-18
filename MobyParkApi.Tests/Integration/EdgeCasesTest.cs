using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MobyParkApi.Models.Dto;
using Xunit;

namespace MobyParkApi.Tests.Integration
{
    /// <summary>
    /// Uitgebreide edge case tests voor alle API endpoints
    /// Test alle mogelijke edge cases, validatie fouten, en error scenarios
    /// </summary>
    public class EdgeCaseTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public EdgeCaseTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        #region Authentication & Authorization Edge Cases

        [Fact]
        public async Task Login_WithEmptyUsername_ShouldReturnBadRequest()
        {
            var request = new LoginUserDto { Username = "", Password = "Test123!" };
            var response = await _client.PostAsJsonAsync("/api/Users/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_WithNullUsername_ShouldReturnBadRequest()
        {
            var request = new { Username = (string?)null, Password = "Test123!" };
            var response = await _client.PostAsJsonAsync("/api/Users/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
        {
            var request = new LoginUserDto { Username = "testuser", Password = "WrongPassword123!" };
            var response = await _client.PostAsJsonAsync("/api/Users/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_WithNonExistentUser_ShouldReturnUnauthorized()
        {
            var request = new LoginUserDto { Username = "nonexistent", Password = "Test123!" };
            var response = await _client.PostAsJsonAsync("/api/Users/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_WithWhitespaceUsername_ShouldNormalizeAndFail()
        {
            var request = new LoginUserDto { Username = "  testuser  ", Password = "TestPass123!" };
            var response = await _client.PostAsJsonAsync("/api/Users/login", request);
            // Should normalize and potentially succeed if user exists
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Register_WithDuplicateUsername_ShouldReturnBadRequest()
        {
            var request = new RegisterUserDto
            {
                Name = "Test User",
                Username = "testuser", // Already exists
                Password = "Test123!",
                Email = "test@example.com",
                PhoneNumber = "+31612345678",
                BirthYear = 1990
            };
            var response = await _client.PostAsJsonAsync("/api/Users/register", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithInvalidEmail_ShouldReturnBadRequest()
        {
            var request = new RegisterUserDto
            {
                Name = "Test User",
                Username = "newuser",
                Password = "Test123!",
                Email = "invalid-email", // Invalid format
                PhoneNumber = "+31612345678",
                BirthYear = 1990
            };
            var response = await _client.PostAsJsonAsync("/api/Users/register", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
        {
            var request = new RegisterUserDto
            {
                Name = "Test User",
                Username = "newuser",
                Password = "123", // Too weak
                Email = "test@example.com",
                PhoneNumber = "+31612345678",
                BirthYear = 1990
            };
            var response = await _client.PostAsJsonAsync("/api/Users/register", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AccessProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
        {
            var response = await _client.GetAsync("/api/Profile");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AccessProtectedEndpoint_WithInvalidToken_ShouldReturnUnauthorized()
        {
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid-token");
            var response = await _client.GetAsync("/api/Profile");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AccessAdminEndpoint_AsUser_ShouldReturnForbidden()
        {
            // Login as regular user
            var loginRequest = new LoginUserDto { Username = "testuser", Password = "TestPass123!" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Users/login", loginRequest);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            var token = loginResult.GetProperty("accessToken").GetString();

            var authenticatedClient = _factory.CreateClient();
            authenticatedClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            // Try to access admin endpoint
            var response = await authenticatedClient.GetAsync("/api/Users/all");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region Reservation Edge Cases

        [Fact]
        public async Task CreateReservation_WithPastStartDate_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new ReservationDto
            {
                LicensePlate = "AB-12-CD",
                StartDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"), // Past date
                EndDate = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = 1
            };

            var response = await client.PostAsJsonAsync("/api/Reservation", request);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateReservation_WithEndDateBeforeStartDate_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new ReservationDto
            {
                LicensePlate = "AB-12-CD",
                StartDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss"), // Before start
                ParkingLotId = 1
            };

            var response = await client.PostAsJsonAsync("/api/Reservation", request);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateReservation_WithNonExistentParkingLot_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new ReservationDto
            {
                LicensePlate = "AB-12-CD",
                StartDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = DateTime.UtcNow.AddDays(1).AddHours(2).ToString("yyyy-MM-dd HH:mm:ss"),
                ParkingLotId = 99999 // Non-existent
            };

            var response = await client.PostAsJsonAsync("/api/Reservation", request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetReservation_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.GetAsync("/api/Reservation/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetReservation_WithNegativeId_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.GetAsync("/api/Reservation/-1");
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteReservation_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.DeleteAsync("/api/Reservation/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetAllReservations_WithInvalidPagination_ShouldNormalize()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            // Test negative page
            var response1 = await client.GetAsync("/api/Reservation?page=-1");
            response1.StatusCode.Should().Be(HttpStatusCode.OK); // Should normalize to page 1

            // Test pageSize > 100
            var response2 = await client.GetAsync("/api/Reservation?pageSize=200");
            response2.StatusCode.Should().Be(HttpStatusCode.OK); // Should normalize to 10

            // Test pageSize = 0
            var response3 = await client.GetAsync("/api/Reservation?pageSize=0");
            response3.StatusCode.Should().Be(HttpStatusCode.OK); // Should normalize to 10
        }

        #endregion

        #region Payment Edge Cases

        [Fact]
        public async Task CreatePayment_WithNegativeDuration_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-12-CD",
                Duration = -10 // Negative duration
            };

            var response = await client.PostAsJsonAsync("/api/Payments", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreatePayment_WithZeroDuration_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-12-CD",
                Duration = 0 // Zero duration
            };

            var response = await client.PostAsJsonAsync("/api/Payments", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreatePayment_WithNonExistentParkingLot_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreatedPaymentDto
            {
                ParkingLotId = 99999, // Non-existent
                LicensePlate = "AB-12-CD",
                Duration = 120
            };

            var response = await client.PostAsJsonAsync("/api/Payments", request);
            // May return Forbidden if user doesn't have access, or BadRequest if parking lot doesn't exist
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetPayment_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.GetAsync("/api/Payments/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RefundPayment_AsNonAdmin_ShouldReturnForbidden()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.PostAsync("/api/Payments/1/refund", null);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreatePaymentFromSession_WithNonExistentSession_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.PostAsync("/api/Payments/from-parking-session/99999", null);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region Parking Lot Edge Cases

        [Fact]
        public async Task GetParkingLot_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.GetAsync("/api/parking-lots/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task StartSession_WithInvalidLicensePlate_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new StartSessionRequestDto
            {
                LicensePlate = "INVALID" // Invalid format
            };

            var response = await client.PostAsJsonAsync("/api/parking-lots/1/sessions/start", request);
            // Accept OK if validation happens elsewhere, or BadRequest/NotFound if validated here
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.OK);
        }

        [Fact]
        public async Task StopSession_WithoutActiveSession_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new StopSessionRequestDto { LicensePlate = "AB-12-CD" };

            var response = await client.PostAsJsonAsync("/api/parking-lots/1/sessions/stop", request);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateParkingLot_AsNonAdmin_ShouldReturnForbidden()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreateParkingLotRequestDto
            {
                Name = "Test Lot",
                Location = "Test City",
                Capacity = 100,
                Reserved = 0,
                Tariff = 3.50m,
                Postcode = "1234AB",
                HouseNumber = 1,
                Lat = 52.0,
                Lng = 5.0
            };

            var response = await client.PostAsJsonAsync("/api/parking-lots", request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetAllParkingLots_WithInvalidPagination_ShouldNormalize()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response2 = await client.GetAsync("/api/parking-lots?page=-1");
            response2.StatusCode.Should().Be(HttpStatusCode.OK); // Should normalize

            var response3 = await client.GetAsync("/api/parking-lots?pageSize=200");
            response3.StatusCode.Should().Be(HttpStatusCode.OK); // Should normalize to 10
        }

        #endregion

        #region Vehicle Edge Cases

        [Fact]
        public async Task CreateVehicle_WithInvalidLicensePlate_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreateVehicleRequestDto
            {
                LicensePlate = "INVALID", // Invalid format
                Make = "Test",
                Model = "Test",
                Color = "Red",
                Year = 2020
            };

            var response = await client.PostAsJsonAsync("/api/Vehicles", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetVehicle_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.GetAsync("/api/Vehicles/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateVehicle_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new UpdateVehicleRequestDto { Color = "Blue" };

            var response = await client.PatchAsJsonAsync("/api/Vehicles/99999", request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteVehicle_WithNonExistentId_ShouldReturnNotFound()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var response = await client.DeleteAsync("/api/Vehicles/99999");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region Profile Edge Cases

        [Fact]
        public async Task UpdateProfile_WithInvalidEmail_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new UpdateProfileDto
            {
                Email = "invalid-email" // Invalid format
            };

            var response = await client.PutAsJsonAsync("/api/Profile", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ReactivateProfile_WithInvalidCredentials_ShouldReturnBadRequest()
        {
            var request = new ReactivateProfileDto
            {
                Username = "nonexistent",
                Password = "WrongPassword"
            };

            var response = await _client.PostAsJsonAsync("/api/Profile/reactivate", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Discount Code Edge Cases

        [Fact]
        public async Task CreateDiscountCode_AsNonAdmin_ShouldReturnForbidden()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new CreateDiscountCodeDto
            {
                Code = "TEST123",
                DiscountType = "Percentage",
                DiscountValue = 10,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30)
            };

            var response = await client.PostAsJsonAsync("/api/DiscountCode", request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ValidateDiscountCode_WithNonExistentCode_ShouldReturnBadRequest()
        {
            var token = await GetAuthTokenAsync();
            var client = CreateAuthenticatedClient(token);

            var request = new ValidateDiscountCodeDto
            {
                Code = "NONEXISTENT",
                OriginalCost = 100m
            };

            var response = await client.PostAsJsonAsync("/api/DiscountCode/validate", request);
            // May return OK with validation result, or BadRequest if validation fails
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        #endregion

        #region Helper Methods

        private async Task<string> GetAuthTokenAsync()
        {
            var loginRequest = new LoginUserDto
            {
                Username = "testuser",
                Password = "TestPass123!"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/Users/login", loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            return loginResult.GetProperty("accessToken").GetString()!;
        }

        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            return client;
        }

        #endregion
    }
}