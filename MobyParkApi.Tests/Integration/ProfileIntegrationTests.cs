using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using System.Threading.Tasks;
using MobyParkApi.Models.Dto;
using Xunit;

namespace MobyParkApi.Tests.Integration
{
	public class ProfileIntegrationTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly CustomWebApplicationFactory _factory;

		public ProfileIntegrationTests(CustomWebApplicationFactory factory)
		{
			_factory = factory;
		}

		[Fact]
		public async Task GetProfile_Returns_Profile_For_Authenticated_User()
		{
			var client = await GetAuthenticatedClientAsync();
			var res = await client.GetAsync("/api/profile");
			res.EnsureSuccessStatusCode();
			var json = await res.Content.ReadFromJsonAsync<JsonElement>();
			json.GetProperty("username").GetString().Should().Be("testuser");
		}

		[Fact]
		public async Task PutProfile_Updates_Fields()
		{
			var client = await GetAuthenticatedClientAsync();
			var update = new UpdateProfileDto
			{
				Name = "Updated User",
				Email = "updated@example.com",
				PhoneNumber = "0612345678",
				BirthYear = 1991
			};
			var res = await client.PutAsJsonAsync("/api/profile", update);
			res.EnsureSuccessStatusCode();

			var res2 = await client.GetAsync("/api/profile");
			res2.EnsureSuccessStatusCode();
			var json = await res2.Content.ReadFromJsonAsync<JsonElement>();
			json.GetProperty("name").GetString().Should().Be("Updated User");
			json.GetProperty("email").GetString().Should().Be("updated@example.com");
		}

		[Fact]
		public async Task DeleteProfile_Sets_Inactive()
		{
			var client = await GetAuthenticatedClientAsync();
			var res = await client.DeleteAsync("/api/profile");
			res.EnsureSuccessStatusCode();

			// After deactivating profile, user may not be able to access profile anymore
			// Try to get profile - may return Unauthorized if user is inactive
			var res2 = await client.GetAsync("/api/profile");
			
			// Accept both OK (if still accessible) and Unauthorized (if user is inactive)
			if (res2.StatusCode == System.Net.HttpStatusCode.OK)
			{
				var json = await res2.Content.ReadFromJsonAsync<JsonElement>();
				json.GetProperty("active").GetBoolean().Should().BeFalse();
			}
			else
			{
				// If Unauthorized, that's also valid - inactive users can't access their profile
				res2.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
			}
		}

		private async Task<HttpClient> GetAuthenticatedClientAsync()
		{
			// Ensure test user exists and is active before obtaining token
			_factory.EnsureTestUserExists();
			var client = _factory.CreateClient();
			var loginRequest = new LoginUserDto { Username = "testuser", Password = "TestPass123!" };
			var loginResponse = await client.PostAsJsonAsync("/api/Users/login", loginRequest);
			loginResponse.EnsureSuccessStatusCode();
			var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
			var token = loginResult.GetProperty("accessToken").GetString();
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			return client;
		}
	}
}


