using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MobyParkApi.Controllers;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit;

namespace MobyParkApi.Tests.Controllers
{
    public class DiscountCodeControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDiscountCodeService> _mockDiscountCodeService;
        private readonly Mock<ILogger<DiscountCodeController>> _mockLogger;
        private readonly DiscountCodeController _controller;

        public DiscountCodeControllerTests()
        {
            // Setup InMemory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            // Setup mocks
            _mockDiscountCodeService = new Mock<IDiscountCodeService>();
            _mockLogger = new Mock<ILogger<DiscountCodeController>>();

            // Create controller with mocked dependencies
            _controller = new DiscountCodeController(
                _mockDiscountCodeService.Object,
                _context,
                _mockLogger.Object
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Helper Methods

        private void SetupUser(int userId, string username, string role)
        {
            // Setup claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Add user to database
            var user = new Users
            {
                Id = userId,
                Username = username,
                Role = role,
                Name = username,
                Email = $"{username}@test.com",
                Phone_Number = "0612345678",
                Password = "hashedpassword",
                Birth_Year = 1990,
                Active = true,
                Created_At = DateTime.UtcNow,
                Modified_At = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.SaveChanges();
        }

        private CreateDiscountCodeDto CreateDiscountCodeDto(
            string code = "TEST123",
            string discountType = "Percentage",
            decimal discountValue = 10.0m)
        {
            return new CreateDiscountCodeDto
            {
                Code = code,
                DiscountType = discountType,
                DiscountValue = discountValue,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                MaxUses = 100
            };
        }

        private DiscountCodeResponseDto CreateDiscountCodeResponseDto(int id = 1)
        {
            return new DiscountCodeResponseDto
            {
                Id = id,
                Code = "TEST123",
                DiscountType = "Percentage",
                DiscountValue = 10.0m,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                MaxUses = 100,
                CurrentUses = 0,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region POST /api/DiscountCode Tests

        [Fact]
        public async Task CreateDiscountCode_AdminUser_CreatesSuccessfully()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var dto = CreateDiscountCodeDto();
            var expectedResponse = CreateDiscountCodeResponseDto();

            _mockDiscountCodeService
                .Setup(s => s.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateDiscountCode(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var discountCode = Assert.IsType<DiscountCodeResponseDto>(createdResult.Value);

            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(expectedResponse.Id, discountCode.Id);
            Assert.Equal("TEST123", discountCode.Code);

            _mockDiscountCodeService.Verify(
                s => s.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>(), 1),
                Times.Once
            );
        }

        [Fact]
        public async Task CreateDiscountCode_DuplicateCode_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var dto = CreateDiscountCodeDto();

            _mockDiscountCodeService
                .Setup(s => s.CreateDiscountCodeAsync(It.IsAny<CreateDiscountCodeDto>(), It.IsAny<int>()))
                .ThrowsAsync(new ArgumentException("Kortingscode bestaat al"));

            // Act
            var result = await _controller.CreateDiscountCode(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);

            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Kortingscode bestaat al", errorProperty?.ToString());
        }

        [Fact]
        public async Task CreateDiscountCode_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var dto = CreateDiscountCodeDto();
            _controller.ModelState.AddModelError("Code", "Code is verplicht");

            // Act
            var result = await _controller.CreateDiscountCode(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region GET /api/DiscountCode/{id} Tests

        [Fact]
        public async Task GetDiscountCodeById_ExistingCode_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var expectedResponse = CreateDiscountCodeResponseDto();

            _mockDiscountCodeService
                .Setup(s => s.GetDiscountCodeByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetDiscountCodeById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var discountCode = Assert.IsType<DiscountCodeResponseDto>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(expectedResponse.Id, discountCode.Id);
        }

        [Fact]
        public async Task GetDiscountCodeById_NonExistentCode_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");

            _mockDiscountCodeService
                .Setup(s => s.GetDiscountCodeByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((DiscountCodeResponseDto?)null);

            // Act
            var result = await _controller.GetDiscountCodeById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value;
            var errorProperty = errorResponse?.GetType().GetProperty("error")?.GetValue(errorResponse, null);

            Assert.Equal(404, notFoundResult.StatusCode);
            Assert.Equal("Kortingscode niet gevonden", errorProperty?.ToString());
        }

        #endregion

        #region GET /api/DiscountCode Tests

        [Fact]
        public async Task GetAllDiscountCodes_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var expectedResponse = new List<DiscountCodeResponseDto>
            {
                CreateDiscountCodeResponseDto(1),
                CreateDiscountCodeResponseDto(2)
            };

            _mockDiscountCodeService
                .Setup(s => s.GetAllDiscountCodesAsync(It.IsAny<bool?>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetAllDiscountCodes(null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var discountCodes = Assert.IsType<List<DiscountCodeResponseDto>>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(2, discountCodes.Count);
        }

        [Fact]
        public async Task GetAllDiscountCodes_ActiveOnly_ReturnsFilteredResults()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var expectedResponse = new List<DiscountCodeResponseDto>
            {
                CreateDiscountCodeResponseDto(1)
            };

            _mockDiscountCodeService
                .Setup(s => s.GetAllDiscountCodesAsync(true))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetAllDiscountCodes(activeOnly: true);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var discountCodes = Assert.IsType<List<DiscountCodeResponseDto>>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Single(discountCodes);
        }

        #endregion

        #region PUT /api/DiscountCode/{id} Tests

        [Fact]
        public async Task UpdateDiscountCode_ExistingCode_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var updateDto = new UpdateDiscountCodeDto
            {
                DiscountValue = 20.0m,
                IsActive = false
            };
            var expectedResponse = CreateDiscountCodeResponseDto();
            expectedResponse.DiscountValue = 20.0m;
            expectedResponse.IsActive = false;

            _mockDiscountCodeService
                .Setup(s => s.UpdateDiscountCodeAsync(It.IsAny<int>(), It.IsAny<UpdateDiscountCodeDto>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateDiscountCode(1, updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var discountCode = Assert.IsType<DiscountCodeResponseDto>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(20.0m, discountCode.DiscountValue);
            Assert.False(discountCode.IsActive);
        }

        [Fact]
        public async Task UpdateDiscountCode_NonExistentCode_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var updateDto = new UpdateDiscountCodeDto { DiscountValue = 20.0m };

            _mockDiscountCodeService
                .Setup(s => s.UpdateDiscountCodeAsync(It.IsAny<int>(), It.IsAny<UpdateDiscountCodeDto>()))
                .ThrowsAsync(new KeyNotFoundException("Kortingscode niet gevonden"));

            // Act
            var result = await _controller.UpdateDiscountCode(999, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task UpdateDiscountCode_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var updateDto = new UpdateDiscountCodeDto();
            _controller.ModelState.AddModelError("DiscountValue", "Invalid value");

            // Act
            var result = await _controller.UpdateDiscountCode(1, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region DELETE /api/DiscountCode/{id} Tests

        [Fact]
        public async Task DeactivateDiscountCode_ExistingCode_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");

            _mockDiscountCodeService
                .Setup(s => s.DeactivateDiscountCodeAsync(It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeactivateDiscountCode(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            var messageProperty = response?.GetType().GetProperty("message")?.GetValue(response, null);
            var idProperty = response?.GetType().GetProperty("id")?.GetValue(response, null);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Contains("succesvol gedeactiveerd", messageProperty?.ToString());
            Assert.Equal(1, idProperty);

            _mockDiscountCodeService.Verify(
                s => s.DeactivateDiscountCodeAsync(1),
                Times.Once
            );
        }

        [Fact]
        public async Task DeactivateDiscountCode_NonExistentCode_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");

            _mockDiscountCodeService
                .Setup(s => s.DeactivateDiscountCodeAsync(It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException("Kortingscode niet gevonden"));

            // Act
            var result = await _controller.DeactivateDiscountCode(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        #endregion

        #region POST /api/DiscountCode/validate Tests

        [Fact]
        public async Task ValidateDiscountCode_ValidCode_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "user", "User");
            var validateDto = new ValidateDiscountCodeDto
            {
                Code = "TEST123",
                ParkingLotId = 1,
                ReservationStartTime = DateTime.UtcNow.AddDays(1),
                OriginalCost = 100.0m
            };
            var expectedResponse = new DiscountCodeValidationResultDto
            {
                IsValid = true,
                DiscountAmount = 10.0m,
                FinalCost = 90.0m,
                DiscountCode = CreateDiscountCodeResponseDto()
            };

            _mockDiscountCodeService
                .Setup(s => s.ValidateDiscountCodeAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<decimal>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ValidateDiscountCode(validateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var validationResult = Assert.IsType<DiscountCodeValidationResultDto>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.True(validationResult.IsValid);
            Assert.Equal(10.0m, validationResult.DiscountAmount);
            Assert.Equal(90.0m, validationResult.FinalCost);
        }

        [Fact]
        public async Task ValidateDiscountCode_InvalidCode_ReturnsOkWithInvalidResult()
        {
            // Arrange
            SetupUser(1, "user", "User");
            var validateDto = new ValidateDiscountCodeDto
            {
                Code = "INVALID",
                ParkingLotId = 1,
                ReservationStartTime = DateTime.UtcNow.AddDays(1),
                OriginalCost = 100.0m
            };
            var expectedResponse = new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode niet gevonden"
            };

            _mockDiscountCodeService
                .Setup(s => s.ValidateDiscountCodeAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<decimal>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ValidateDiscountCode(validateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var validationResult = Assert.IsType<DiscountCodeValidationResultDto>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.False(validationResult.IsValid);
            Assert.Contains("niet gevonden", validationResult.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDiscountCode_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(1, "user", "User");
            var validateDto = new ValidateDiscountCodeDto();
            _controller.ModelState.AddModelError("Code", "Code is verplicht");

            // Act
            var result = await _controller.ValidateDiscountCode(validateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region GET /api/DiscountCode/{id}/statistics Tests

        [Fact]
        public async Task GetDiscountCodeStatistics_ExistingCode_ReturnsOk()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");
            var expectedStatistics = new DiscountCodeStatisticsDto
            {
                TotalUses = 5,
                TotalDiscountAmount = 50.0m,
                TotalOriginalAmount = 500.0m,
                ConversionRate = 100.0m
            };

            _mockDiscountCodeService
                .Setup(s => s.GetDiscountCodeStatisticsAsync(It.IsAny<int>()))
                .ReturnsAsync(expectedStatistics);

            // Act
            var result = await _controller.GetDiscountCodeStatistics(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var statistics = Assert.IsType<DiscountCodeStatisticsDto>(okResult.Value);

            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(5, statistics.TotalUses);
            Assert.Equal(50.0m, statistics.TotalDiscountAmount);
        }

        [Fact]
        public async Task GetDiscountCodeStatistics_NonExistentCode_ReturnsNotFound()
        {
            // Arrange
            SetupUser(1, "admin", "Admin");

            _mockDiscountCodeService
                .Setup(s => s.GetDiscountCodeStatisticsAsync(It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException("Kortingscode niet gevonden"));

            // Act
            var result = await _controller.GetDiscountCodeStatistics(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        #endregion
    }
}
