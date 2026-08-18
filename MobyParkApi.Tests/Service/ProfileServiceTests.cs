using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;
using FluentAssertions;

namespace MobyParkApi.Tests.Services
{
	public class ProfileServiceTests : IDisposable
	{
		private readonly ApplicationDbContext _context;
		private readonly ProfileService _service;

		public ProfileServiceTests()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;

			_context = new ApplicationDbContext(options);
			_service = new ProfileService(_context);
		}

		public void Dispose()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

		#region Helper Methods

		private Users CreateTestUser(int id = 1, string name = "Test User", string email = "test@example.com")
		{
			return new Users
			{
				Id = id,
				Name = name,
				Username = $"testuser{id}",
				Email = email,
				Phone_Number = "0612345678",
				Birth_Year = 1990,
				Role = "User",
				Active = true,
				Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
				Created_At = DateTime.UtcNow,
				Modified_At = DateTime.UtcNow
			};
		}

		#endregion

		#region GetProfileAsync Tests

		[Fact]
		public async Task GetProfileAsync_Returns_User_When_Exists()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetProfileAsync(1);

			// Assert
			result.Should().NotBeNull();
			result!.Id.Should().Be(1);
			result.Name.Should().Be("Test User");
			result.Email.Should().Be("test@example.com");
		}

		[Fact]
		public async Task GetProfileAsync_Returns_Null_When_User_Not_Exists()
		{
			// Arrange
			// No users in database

			// Act
			var result = await _service.GetProfileAsync(999);

			// Assert
			result.Should().BeNull();
		}

		[Fact]
		public async Task GetProfileAsync_Returns_Correct_User_When_Multiple_Users_Exist()
		{
			// Arrange
			_context.Users.Add(CreateTestUser(1, "User 1", "user1@example.com"));
			_context.Users.Add(CreateTestUser(2, "User 2", "user2@example.com"));
			_context.Users.Add(CreateTestUser(3, "User 3", "user3@example.com"));
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.GetProfileAsync(2);

			// Assert
			result.Should().NotBeNull();
			result!.Id.Should().Be(2);
			result.Name.Should().Be("User 2");
			result.Email.Should().Be("user2@example.com");
		}

		#endregion

		#region UpdateProfileAsync Tests

		[Fact]
		public async Task UpdateProfileAsync_Updates_Name()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto { Name = "Updated Name" };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be("Updated Name");
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Name.Should().Be("Updated Name");
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_Email()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto { Email = "updated@example.com" };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Email.Should().Be("updated@example.com");
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Email.Should().Be("updated@example.com");
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_PhoneNumber()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto { PhoneNumber = "0698765432" };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Phone_Number.Should().Be("0698765432");
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Phone_Number.Should().Be("0698765432");
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_BirthYear()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto { BirthYear = 1995 };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Birth_Year.Should().Be(1995);
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Birth_Year.Should().Be(1995);
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_Password_And_Hashes_It()
		{
			// Arrange
			var user = CreateTestUser();
			var originalPassword = user.Password;
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto { Password = "NewPassword123!" };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Password.Should().NotBe(originalPassword);
			result.Password.Should().NotBe("NewPassword123!"); // Should be hashed
			
			var userInDb = await _context.Users.FindAsync(1);
			BCrypt.Net.BCrypt.Verify("NewPassword123!", userInDb!.Password).Should().BeTrue();
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_Multiple_Fields()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto
			{
				Name = "Updated Name",
				Email = "updated@example.com",
				PhoneNumber = "0611111111",
				BirthYear = 1992
			};

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be("Updated Name");
			result.Email.Should().Be("updated@example.com");
			result.Phone_Number.Should().Be("0611111111");
			result.Birth_Year.Should().Be(1992);
		}

		[Fact]
		public async Task UpdateProfileAsync_Trims_String_Fields()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var dto = new UpdateProfileDto
			{
				Name = "  Trimmed Name  ",
				Email = "  trimmed@example.com  ",
				PhoneNumber = "  0612345678  "
			};

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be("Trimmed Name");
			result.Email.Should().Be("trimmed@example.com");
			result.Phone_Number.Should().Be("0612345678");
		}

		[Fact]
		public async Task UpdateProfileAsync_Does_Not_Update_When_Field_Is_Null()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var originalName = user.Name;
			var originalEmail = user.Email;

			var dto = new UpdateProfileDto { BirthYear = 1995 };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be(originalName);
			result.Email.Should().Be(originalEmail);
			result.Birth_Year.Should().Be(1995);
		}

		[Fact]
		public async Task UpdateProfileAsync_Does_Not_Update_When_Field_Is_Empty_String()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var originalName = user.Name;

			var dto = new UpdateProfileDto { Name = "" };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be(originalName); // Should not change
		}

		[Fact]
		public async Task UpdateProfileAsync_Does_Not_Update_When_Field_Is_Whitespace()
		{
			// Arrange
			var user = CreateTestUser();
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var originalName = user.Name;

			var dto = new UpdateProfileDto { Name = "   " };

			// Act
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Name.Should().Be(originalName); // Should not change
		}

		[Fact]
		public async Task UpdateProfileAsync_Updates_Modified_At()
		{
			// Arrange
			var user = CreateTestUser();
			user.Modified_At = DateTime.UtcNow.AddDays(-1);
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var originalModifiedAt = user.Modified_At;
			var dto = new UpdateProfileDto { Name = "Updated" };

			// Act
			await Task.Delay(100); // Small delay to ensure timestamp difference
			var result = await _service.UpdateProfileAsync(1, dto);

			// Assert
			result.Should().NotBeNull();
			result!.Modified_At.Should().BeAfter(originalModifiedAt ?? DateTime.MinValue);
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Modified_At.Should().BeAfter(originalModifiedAt ?? DateTime.MinValue);
		}

		[Fact]
		public async Task UpdateProfileAsync_Returns_Null_When_User_Not_Found()
		{
			// Arrange
			// No users in database
			var dto = new UpdateProfileDto { Name = "Updated" };

			// Act
			var result = await _service.UpdateProfileAsync(999, dto);

			// Assert
			result.Should().BeNull();
		}

		#endregion

		#region DeactivateProfileAsync Tests

		[Fact]
		public async Task DeactivateProfileAsync_Sets_Active_To_False()
		{
			// Arrange
			var user = CreateTestUser();
			user.Active = true;
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.DeactivateProfileAsync(1);

			// Assert
			result.Should().BeTrue();
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Active.Should().BeFalse();
		}

		[Fact]
		public async Task DeactivateProfileAsync_Updates_Modified_At()
		{
			// Arrange
			var user = CreateTestUser();
			user.Modified_At = DateTime.UtcNow.AddDays(-1);
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var originalModifiedAt = user.Modified_At;

			// Act
			await Task.Delay(100); // Small delay to ensure timestamp difference
			var result = await _service.DeactivateProfileAsync(1);

			// Assert
			result.Should().BeTrue();
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Modified_At.Should().BeAfter(originalModifiedAt ?? DateTime.MinValue);
		}

		[Fact]
		public async Task DeactivateProfileAsync_Returns_False_When_User_Not_Found()
		{
			// Arrange
			// No users in database

			// Act
			var result = await _service.DeactivateProfileAsync(999);

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public async Task DeactivateProfileAsync_Works_When_User_Already_Inactive()
		{
			// Arrange
			var user = CreateTestUser();
			user.Active = false;
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			// Act
			var result = await _service.DeactivateProfileAsync(1);

			// Assert
			result.Should().BeTrue();
			
			var userInDb = await _context.Users.FindAsync(1);
			userInDb!.Active.Should().BeFalse();
		}

		#endregion
	}
}

