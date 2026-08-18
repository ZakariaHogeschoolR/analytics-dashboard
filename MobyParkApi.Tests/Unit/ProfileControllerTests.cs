using System;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using Xunit;
using MobyParkApi.Services;

namespace MobyParkApi.Tests.Unit
{
	public class ProfileControllerTests
	{
		private static ApplicationDbContext CreateDb()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options;
			var db = new ApplicationDbContext(options);
			db.Users.Add(new Users
			{
				Id = 1,
				Name = "Test User",
				Username = "testuser",
				Email = "test@example.com",
				Phone_Number = "0612345678",
				Birth_Year = 1990,
				Role = "User",
				Active = true,
				Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!")
			});
			db.SaveChanges();
			return db;
		}

		private static ProfileController CreateController(ApplicationDbContext db)
		{
			var service = new ProfileService(db);
			var controller = new ProfileController(service);
			var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, "1"),
				new Claim(ClaimTypes.Name, "testuser"),
				new Claim(ClaimTypes.Role, "User")
			}, "TestAuth"));
			controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = user }
			};
			return controller;
		}

	[Fact]
	public async Task GetProfile_Returns_Ok()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var result = await controller.GetProfile();
		result.Should().BeOfType<OkObjectResult>();
	}

	[Fact]
	public async Task GetProfile_Returns_NotFound_When_User_Not_Found()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "999"),
			new Claim(ClaimTypes.Name, "nonexistent"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var result = await controller.GetProfile();
		result.Should().BeOfType<NotFoundObjectResult>();
		var notFoundResult = result as NotFoundObjectResult;
		notFoundResult!.Value.Should().Be("Gebruiker niet gevonden");
	}

	[Fact]
	public async Task GetProfile_Returns_Unauthorized_When_No_UserId()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.Name, "testuser"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var result = await controller.GetProfile();
		result.Should().BeOfType<UnauthorizedObjectResult>();
		var unauthorizedResult = result as UnauthorizedObjectResult;
		unauthorizedResult!.Value.Should().Be("Geen geldige gebruiker gevonden");
	}

	[Fact]
	public async Task GetProfile_Returns_Correct_Profile_Data()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var result = await controller.GetProfile();
		
		result.Should().BeOfType<OkObjectResult>();
		var okResult = result as OkObjectResult;
		var profile = okResult!.Value;
		
		profile.Should().NotBeNull();
		var profileType = profile!.GetType();
		profileType.GetProperty("id")!.GetValue(profile).Should().Be(1);
		profileType.GetProperty("name")!.GetValue(profile).Should().Be("Test User");
		profileType.GetProperty("username")!.GetValue(profile).Should().Be("testuser");
		profileType.GetProperty("email")!.GetValue(profile).Should().Be("test@example.com");
	}

	[Fact]
	public async Task UpdateProfile_Changes_Name()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto { Name = "Changed" };
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Name.Should().Be("Changed");
	}

	[Fact]
	public async Task UpdateProfile_Updates_Email()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto { Email = "newemail@example.com" };
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Email.Should().Be("newemail@example.com");
	}

	[Fact]
	public async Task UpdateProfile_Updates_PhoneNumber()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto { PhoneNumber = "0698765432" };
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Phone_Number.Should().Be("0698765432");
	}

	[Fact]
	public async Task UpdateProfile_Updates_BirthYear()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto { BirthYear = 1995 };
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Birth_Year.Should().Be(1995);
	}

	[Fact]
	public async Task UpdateProfile_Updates_Password()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var originalPassword = (await db.Users.FirstAsync(u => u.Id == 1)).Password;
		var dto = new UpdateProfileDto { Password = "NewPassword123!" };
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Password.Should().NotBe(originalPassword);
		BCrypt.Net.BCrypt.Verify("NewPassword123!", user.Password).Should().BeTrue();
	}

	[Fact]
	public async Task UpdateProfile_Updates_Multiple_Fields()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto 
		{ 
			Name = "Updated Name",
			Email = "updated@example.com",
			PhoneNumber = "0611111111",
			BirthYear = 1992
		};
		var res = await controller.UpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Name.Should().Be("Updated Name");
		user.Email.Should().Be("updated@example.com");
		user.Phone_Number.Should().Be("0611111111");
		user.Birth_Year.Should().Be(1992);
	}

	[Fact]
	public async Task UpdateProfile_Returns_NotFound_When_User_Not_Found()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "999"),
			new Claim(ClaimTypes.Name, "nonexistent"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var dto = new UpdateProfileDto { Name = "Changed" };
		var result = await controller.UpdateProfile(dto);
		result.Should().BeOfType<NotFoundObjectResult>();
		var notFoundResult = result as NotFoundObjectResult;
		notFoundResult!.Value.Should().Be("Gebruiker niet gevonden");
	}

	[Fact]
	public async Task UpdateProfile_Returns_Unauthorized_When_No_UserId()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.Name, "testuser"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var dto = new UpdateProfileDto { Name = "Changed" };
		var result = await controller.UpdateProfile(dto);
		result.Should().BeOfType<UnauthorizedObjectResult>();
		var unauthorizedResult = result as UnauthorizedObjectResult;
		unauthorizedResult!.Value.Should().Be("Geen geldige gebruiker gevonden");
	}

	[Fact]
	public async Task CreateOrUpdateProfile_Works_Same_As_Update()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var dto = new UpdateProfileDto { Name = "CreatedOrUpdated" };
		var res = await controller.CreateOrUpdateProfile(dto);
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Name.Should().Be("CreatedOrUpdated");
	}

	[Fact]
	public async Task CreateOrUpdateProfile_Returns_NotFound_When_User_Not_Found()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "999"),
			new Claim(ClaimTypes.Name, "nonexistent"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var dto = new UpdateProfileDto { Name = "Changed" };
		var result = await controller.CreateOrUpdateProfile(dto);
		result.Should().BeOfType<NotFoundObjectResult>();
	}

	[Fact]
	public async Task DeleteProfile_Sets_Active_False()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var res = await controller.DeleteProfile();
		res.Should().BeOfType<OkObjectResult>();
		var user = await db.Users.FirstAsync(u => u.Id == 1);
		user.Active.Should().BeFalse();
	}

	[Fact]
	public async Task DeleteProfile_Returns_NotFound_When_User_Not_Found()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "999"),
			new Claim(ClaimTypes.Name, "nonexistent"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var result = await controller.DeleteProfile();
		result.Should().BeOfType<NotFoundObjectResult>();
		var notFoundResult = result as NotFoundObjectResult;
		notFoundResult!.Value.Should().Be("Gebruiker niet gevonden");
	}

	[Fact]
	public async Task DeleteProfile_Returns_Unauthorized_When_No_UserId()
	{
		using var db = CreateDb();
		var service = new ProfileService(db);
		var controller = new ProfileController(service);
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.Name, "testuser"),
			new Claim(ClaimTypes.Role, "User")
		}, "TestAuth"));
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = user }
		};

		var result = await controller.DeleteProfile();
		result.Should().BeOfType<UnauthorizedObjectResult>();
		var unauthorizedResult = result as UnauthorizedObjectResult;
		unauthorizedResult!.Value.Should().Be("Geen geldige gebruiker gevonden");
	}

	[Fact]
	public async Task DeleteProfile_Updates_Modified_At()
	{
		using var db = CreateDb();
		var controller = CreateController(db);
		var userBefore = await db.Users.FirstAsync(u => u.Id == 1);
		var originalModifiedAt = userBefore.Modified_At;
		
		await Task.Delay(100); // Small delay to ensure timestamp difference
		
		var res = await controller.DeleteProfile();
		res.Should().BeOfType<OkObjectResult>();
		
		var userAfter = await db.Users.FirstAsync(u => u.Id == 1);
		userAfter.Modified_At.Should().BeAfter(originalModifiedAt ?? DateTime.MinValue);
	}
}
}


