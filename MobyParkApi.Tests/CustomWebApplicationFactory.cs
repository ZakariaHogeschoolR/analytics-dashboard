using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using MobyParkApi.Data;
using MobyParkApi.Models;

namespace MobyParkApi.Tests
{
	public class CustomWebApplicationFactory : WebApplicationFactory<Program>
	{
		/// <summary>
		/// Zorgt ervoor dat testuser altijd bestaat en actief is
		/// </summary>
		public void EnsureTestUserExists()
		{
			using var scope = Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			
			var testUser = db.Users.FirstOrDefault(u => u.Username.ToLower() == "testuser");
			if (testUser == null)
			{
				db.Users.Add(new Users
				{
					Name = "Test User",
					Username = "testuser",
					Email = "test@example.com",
					Phone_Number = "+31612345678",
					Birth_Year = 1990,
					Role = "User",
					Active = true,
					Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!")
				});
			}
			else
			{
				testUser.Active = true;
				testUser.Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
			}
			
			db.SaveChanges();
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseEnvironment("Testing");
			
			// Configureer JWT settings voor end-to-end tests
			builder.ConfigureAppConfiguration(config =>
			{
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					{ "Jwt:Key", "DitIsEenZeerSterkeGeheimeKey123!@#" },
					{ "Jwt:Issuer", "MobyParkApi" },
					{ "Jwt:Audience", "MobyParkApiUsers" }
				});
			});

			builder.ConfigureServices(services =>
			{
				// Vervang de DbContext door een geïsoleerde in-memory database per test-run
				var dbContextDescriptor = services.SingleOrDefault(
					d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
				if (dbContextDescriptor != null)
					services.Remove(dbContextDescriptor);

				var contextDescriptor = services.SingleOrDefault(
					d => d.ServiceType == typeof(ApplicationDbContext));
				if (contextDescriptor != null)
					services.Remove(contextDescriptor);

				var inMemoryName = $"IntegrationTestsDb-{Guid.NewGuid()}";
				services.AddDbContext<ApplicationDbContext>(options =>
					options.UseInMemoryDatabase(inMemoryName)
						.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

				// Register test auth scheme without overriding defaults so JWT stays default
				services.AddAuthentication()
					.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, options => { });

				// Build provider and seed data
				var sp = services.BuildServiceProvider();
				using var scope = sp.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
				db.Database.EnsureCreated();

				// Zorg ervoor dat testuser altijd bestaat (ook als er al andere gebruikers zijn)
				var testUser = db.Users.FirstOrDefault(u => u.Username.ToLower() == "testuser");
				if (testUser == null)
				{
					db.Users.Add(new Users
					{
						// Id auto-generated door InMemory
						Name = "Test User",
						Username = "testuser",
						Email = "test@example.com",
						Phone_Number = "+31612345678",
						Birth_Year = 1990,
						Role = "User",
						Active = true,
						Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!")
					});
				}
				else
				{
					// Zorg ervoor dat testuser actief is en het juiste wachtwoord heeft
					testUser.Active = true;
					testUser.Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
				}

				// Zorg ervoor dat er ten minste één parking lot bestaat
				if (!db.ParkingLots.Any())
				{
					db.ParkingLots.Add(new ParkingLots
					{
						// Id auto-generated
						Name = "Test Parking Lot",
						Location = "Test City",
						Address = "Teststraat 1",
						Capacity = 100,
						Reserved = 0,
						Tariff = 3.50m,
						DayTariff = 10,
						Coordinates = "{\"lat\":52.0,\"lng\":5.0}",
						CreatedAt = DateTime.UtcNow,
						ModifiedAt = DateTime.UtcNow
					});
				}

				db.SaveChanges();
			});
		}
	}
}


