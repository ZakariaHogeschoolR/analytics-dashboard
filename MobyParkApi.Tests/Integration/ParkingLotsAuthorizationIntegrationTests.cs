using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MobyParkApi.Models.Dto;
using Xunit;

namespace MobyParkApi.Tests.Integration
{
	/// <summary>
	/// Integration tests voor authorization van ParkingLots endpoints.
	/// Deze tests verifiëren dat [Authorize(Roles = "Admin")] correct werkt
	/// door gebruik te maken van de volledige ASP.NET Core pipeline.
	/// </summary>
	public class ParkingLotsAuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly CustomWebApplicationFactory _factory;
		private readonly HttpClient _client;

		public ParkingLotsAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
		{
			_factory = factory;
			_client = _factory.CreateClient();
		}

		#region Helper Methods

		private async Task<string> GetUserTokenAsync()
		{
			// Login as regular user (testuser is seeded in CustomWebApplicationFactory)
			var loginRequest = new LoginUserDto
			{
				Username = "testuser",
				Password = "TestPass123!"
			};

			var loginResponse = await _client.PostAsJsonAsync("/api/Users/login", loginRequest);
			loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
			
			var loginResult = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
			return loginResult.GetProperty("accessToken").GetString() ?? string.Empty;
		}

		private HttpClient CreateAuthenticatedClient(string token)
		{
			var client = _factory.CreateClient();
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			return client;
		}

		#endregion

		#region CreateParkingLot Authorization Tests

		[Fact]
		public async Task CreateParkingLot_ReturnsForbidden_WhenUserIsNotAdmin()
		{
			// Arrange
			var userToken = await GetUserTokenAsync();
			var authenticatedClient = CreateAuthenticatedClient(userToken);

			var request = new CreateParkingLotRequestDto
			{
				Name = "New Parking",
				Location = "Amsterdam",
				Postcode = "1000AA",
				HouseNumber = 1,
				Capacity = 200,
				Reserved = 0,
				Tariff = 3.00m,
				DayTariff = 20.00m,
				Lat = 52.3676,
				Lng = 4.9041
			};

			// Act
			var response = await authenticatedClient.PostAsJsonAsync("/api/parking-lots", request);

			// Assert
			// Regular user should not be able to create parking lots
			response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
		}

		#endregion

		#region UpdateParkingLot Authorization Tests

		[Fact]
		public async Task UpdateParkingLot_ReturnsForbidden_WhenUserIsNotAdmin()
		{
			// Arrange
			var userToken = await GetUserTokenAsync();
			var authenticatedClient = CreateAuthenticatedClient(userToken);

			var request = new CreateParkingLotRequestDto
			{
				Name = "Updated Parking",
				Location = "Rotterdam",
				Postcode = "3000AA",
				HouseNumber = 2,
				Capacity = 150,
				Reserved = 5,
				Tariff = 4.00m,
				DayTariff = 30.00m,
				Lat = 51.9244,
				Lng = 4.4777
			};

			// Act
			var response = await authenticatedClient.PutAsJsonAsync("/api/parking-lots/1", request);

			// Assert
			// Regular user should not be able to update parking lots
			response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
		}

		#endregion

		#region DeleteParkingLot Authorization Tests

		[Fact]
		public async Task DeleteParkingLot_ReturnsForbidden_WhenUserIsNotAdmin()
		{
			// Arrange
			var userToken = await GetUserTokenAsync();
			var authenticatedClient = CreateAuthenticatedClient(userToken);

			// Act
			var response = await authenticatedClient.DeleteAsync("/api/parking-lots/1");

			// Assert
			// Regular user should not be able to delete parking lots
			response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
		}

		#endregion
	}
}
