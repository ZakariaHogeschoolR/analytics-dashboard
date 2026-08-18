using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MobyParkApi.Models.Dto;
using Xunit;

namespace MobyParkApi.Tests.Integration
{
    /// <summary>
    /// Integration tests voor Users endpoints - test de volledige HTTP flow
    /// </summary>
    public class UserIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public UserIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        #region Register Tests

        [Fact]
        public async Task Register_GeldigeData_ReturnsOk()
        {
            // Arrange
            var registerRequest = new RegisterUserDto
            {
                Name = "Integration Test User",
                Username = $"integrationuser{Guid.NewGuid().ToString().Replace("-", "")}",
                Password = "TestPass123!",
                Email = $"integration{Guid.NewGuid()}@example.com",
                PhoneNumber = "0612345678",
                BirthYear = 1990
            };

             // Act
            var response = await _client.PostAsJsonAsync("/api/Users/register", registerRequest);

            // Debug: print error als het faalt
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Register failed: {errorContent}");
            }

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Account succesvol aangemaakt");
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange - registreer eerst een gebruiker
            var firstUser = new RegisterUserDto
            {
                Name = "First User",
                Username = $"firstuser{Guid.NewGuid()}",
                Password = "TestPass123!",
                Email = "duplicate@example.com",
                PhoneNumber = "0611111111",
                BirthYear = 1990
            };
            await _client.PostAsJsonAsync("/api/Users/register", firstUser);

            // Probeer tweede gebruiker met zelfde email
            var secondUser = new RegisterUserDto
            {
                Name = "Second User",
                Username = $"seconduser{Guid.NewGuid()}",
                Password = "TestPass123!",
                Email = "duplicate@example.com",
                PhoneNumber = "0622222222",
                BirthYear = 1991
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Users/register", secondUser);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task Login_GeldigeCredentials_ReturnsToken()
        {
            // Arrange
            var loginRequest = new LoginUserDto
            {
                Username = "testuser",
                Password = "TestPass123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Users/login", loginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            result.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
            result.GetProperty("role").GetString().Should().Be("User");
        }

        #endregion
    }
}