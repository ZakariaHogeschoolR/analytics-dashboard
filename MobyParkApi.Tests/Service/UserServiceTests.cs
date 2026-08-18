using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;
using FluentAssertions;

namespace MobyParkApi.Tests.Service
{
    /// <summary>
    /// Unit tests voor UsersService - test alleen de service logica zonder controllers
    /// </summary>
    public class UsersServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UsersService _service;

        public UsersServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new UsersService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private Users CreateTestUser(int id = 1, string username = "testuser", bool active = true, string role = "User")
        {
            return new Users
            {
                Id = id,
                Name = "Test User",
                Username = username,
                Email = "test@example.com",
                Phone_Number = "0612345678",
                Password = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
                Role = role,
                Active = active,
                Created_At = DateTime.UtcNow,
                Modified_At = DateTime.UtcNow
            };
        }

        #endregion

        #region LoginAsync Tests (uitbreiding van LoginTest.cs)

        [Fact]
        public async Task LoginAsync_GedeactiveerdeGebruiker_ReturnsNull()
        {
            // Arrange - maak gedeactiveerde gebruiker
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
            _context.Users.Add(new Users
            {
                Username = "testuser",
                Password = hashedPassword,
                Email = "test@example.com",
                Phone_Number = "0612345678",
                Role = "User",
                Active = false // Gedeactiveerd
            });
            await _context.SaveChangesAsync();

            var dto = new LoginUserDto
            {
                Username = "testuser",
                Password = "TestPass123!"
            };

            // Act & Assert - gedeactiveerde gebruiker gooit exception
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await _service.LoginAsync(dto);
            });
        }

        [Fact]
        public async Task LoginAsync_UsernameCaseInsensitive_ReturnsUser()
        {
            // Arrange - gebruiker met lowercase username
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
            _context.Users.Add(new Users
            {
                Username = "testuser",
                Password = hashedPassword,
                Email = "test@example.com",
                Phone_Number = "0612345678",
                Role = "User",
                Active = true
            });
            await _context.SaveChangesAsync();

            var dto = new LoginUserDto
            {
                Username = "TESTUSER", // Hoofdletters
                Password = "TestPass123!"
            };

            // Act
            var result = await _service.LoginAsync(dto);

            // Assert - moet werken ondanks hoofdletters
            result.Should().NotBeNull();
        }

        #endregion

        #region RegisterAsync Tests (uitbreiding van RegisterTests.cs)

        [Fact]
        public async Task RegisterAsync_EmailWordtLowercaseOpgeslagen()
        {
            // Arrange
            var dto = new RegisterUserDto
            {
                Name = "Test User",
                Username = "testuser",
                Password = "TestPass123!",
                Email = "TEST@EXAMPLE.COM", // Hoofdletters
                PhoneNumber = "0612345678",
                BirthYear = 1990
            };

            // Act
            var result = await _service.RegisterAsync(dto);

            // Assert
            result.Success.Should().BeTrue();
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            userInDb!.Email.Should().Be("test@example.com"); // Moet lowercase zijn
        }

        [Fact]
        public async Task RegisterAsync_UsernameWordtLowercaseOpgeslagen()
        {
            // Arrange
            var dto = new RegisterUserDto
            {
                Name = "Test User",
                Username = "TESTUSER", // Hoofdletters
                Password = "TestPass123!",
                Email = "test@example.com",
                PhoneNumber = "0612345678",
                BirthYear = 1990
            };

            // Act
            var result = await _service.RegisterAsync(dto);

            // Assert
            result.Success.Should().BeTrue();
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            userInDb.Should().NotBeNull();
            userInDb!.Username.Should().Be("testuser"); // Moet lowercase zijn
        }

        #endregion
    }
}