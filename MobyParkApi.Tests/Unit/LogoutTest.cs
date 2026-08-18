using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MobyParkApi.Controllers;
using MobyParkApi.Services;
using MobyParkApi.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;

namespace MobyParkApi.Tests
{
    public class LogoutTests
    {
        [Fact]
        public void Logout_ReturnsSuccessMessage_WhenAuthorized()
        {
            // Arrange
            var mockUserService = new Mock<IUsersService>();
            var mockAuthService = new Mock<IAuthService>();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "LogoutTestDB")
                .Options;
            var context = new ApplicationDbContext(options);

            var controller = new UsersController(mockUserService.Object, mockAuthService.Object, context);

            // 🔹 Mock een ingelogde gebruiker met rol "User"
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, "mock");
            var principal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Act
            var result = controller.Logout() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);

            // ✅ Converteer result.Value naar JSON en lees 'message'
            var json = JsonSerializer.Serialize(result.Value);
            using var doc = JsonDocument.Parse(json);
            var message = doc.RootElement.GetProperty("message").GetString();

            Assert.Equal("Uitloggen succesvol",message);
        }
    }
}
