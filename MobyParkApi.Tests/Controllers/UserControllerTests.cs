using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;
using FluentAssertions;

namespace MobyParkApi.Tests.Controllers
{
    /// <summary>
    /// Unit tests voor UsersController - test controller logica met mocked services
    /// </summary>
    public class UserControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UsersService _usersService;
        private readonly AuthService _authService;
        private readonly UsersController _controller;

        public UserControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _usersService = new UsersService(_context);

            // Mock IConfiguration voor JWT token generatie
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Jwt:Key"]).Returns("TestKey123456789012345678901234567890");
            configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");

            _authService = new AuthService(configMock.Object);
            _controller = new UsersController(_usersService, _authService, _context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SetupUserClaims(int userId, string role = "User")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

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

        private RegisterUserDto CreateValidRegisterDto()
        {
            return new RegisterUserDto
            {
                Name = "New User",
                Username = "newuser",
                Password = "Password123!",
                Email = "new@example.com",
                PhoneNumber = "0611111111",
                BirthYear = 1990
            };
        }

        #endregion

        #region Register Tests

        [Fact]
        public async Task Register_GeldigeData_MaaktAccountAan()
        {
            // Arrange
            var dto = CreateValidRegisterDto();

            // Act
            var result = await _controller.Register(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().Be("Account succesvol aangemaakt ✅");

            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
            userInDb.Should().NotBeNull();
            userInDb!.Email.Should().Be("new@example.com");
            userInDb.Active.Should().BeTrue();
            userInDb.Role.Should().Be("User");
        }

        [Fact]
        public async Task Register_EmailBestaatAl_ReturnsBadRequest()
        {
            // Arrange
            _context.Users.Add(CreateTestUser(1, "existing", true));
            await _context.SaveChangesAsync();

            var dto = CreateValidRegisterDto();
            dto.Email = "test@example.com";

            // Act
            var result = await _controller.Register(dto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("E-mailadres bestaat al");
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task Login_GeldigeCredentials_ReturnsToken()
        {
            // Arrange
            _context.Users.Add(CreateTestUser(1, "testuser", true));
            await _context.SaveChangesAsync();

            var dto = new LoginUserDto
            {
                Username = "testuser",
                Password = "TestPass123!"
            };

            // Act
            var result = await _controller.Login(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value;
            var accessTokenProperty = response!.GetType().GetProperty("accessToken");
            accessTokenProperty.Should().NotBeNull();
            accessTokenProperty!.GetValue(response).Should().NotBeNull();
        }

        [Fact]
        public async Task Login_GedeactiveerdeGebruiker_ReturnsUnauthorized()
        {
            // Arrange
            _context.Users.Add(CreateTestUser(1, "testuser", false));
            await _context.SaveChangesAsync();

            var dto = new LoginUserDto
            {
                Username = "testuser",
                Password = "TestPass123!"
            };

            // Act
            var result = await _controller.Login(dto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().Be("Account is gedeactiveerd");
        }
        #endregion

        #region GetAllUsers Tests

        [Fact]
        public async Task GetAllUsers_AdminRol_ReturnsOk()
        {
            // Arrange
            SetupUserClaims(1, "Admin");
            _context.Users.Add(CreateTestUser(1, "user1", true));
            _context.Users.Add(CreateTestUser(2, "user2", true));
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetAllUsers(1, 10);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
        #endregion

        #region MakeAdmin Tests

        [Fact]
        public async Task MakeAdmin_AdminRol_MaaktGebruikerAdmin()
        {
            // Arrange
            SetupUserClaims(1, "Admin");
            _context.Users.Add(CreateTestUser(2, "regularuser", true, "User"));
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MakeAdmin(2);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var user = await _context.Users.FindAsync(2);
            user!.Role.Should().Be("Admin");
        }

        [Fact]
        public async Task MakeAdmin_GebruikerBestaatNiet_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims(1, "Admin");

            // Act
            var result = await _controller.MakeAdmin(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be("Gebruiker niet gevonden");
        }

        #endregion
    }
}