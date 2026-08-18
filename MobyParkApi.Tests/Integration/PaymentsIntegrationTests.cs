using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.Integration
{
    public class PaymentsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ApplicationDbContext _context;

        public PaymentsIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove bestaande DbContext
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add in-memory database voor tests
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid().ToString());
                    });
                });
            });

            _client = _factory.CreateClient();

            // Get DbContext voor seeding
            var scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            SeedTestData();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _client.Dispose();
        }

        private void SeedTestData()
        {
            // Users
            var users = new[]
            {
                new Users 
                { 
                    Id = 1, 
                    Email = "user@test.com", 
                    Password = "hashed_password",
                    Role = "User", 
                    FirstName = "Test", 
                    LastName = "User",
                    Created_At = DateTime.UtcNow
                },
                new Users 
                { 
                    Id = 2, 
                    Email = "admin@test.com", 
                    Password = "hashed_password",
                    Role = "Admin", 
                    FirstName = "Admin", 
                    LastName = "User",
                    Created_At = DateTime.UtcNow
                }
            };
            _context.Users.AddRange(users);

            // ParkingLots
            var parkingLots = new[]
            {
                new ParkingLots 
                { 
                    Id = 1, 
                    Name = "Test Lot A", 
                    Location = "Center", 
                    Address = "Main St 1",
                    Capacity = 100, 
                    Reserved = 0, 
                    Tariff = 2.50m, 
                    DayTariff = 20.00m,
                    Coordinates = "52.0,4.0"
                }
            };
            _context.ParkingLots.AddRange(parkingLots);

            // Vehicles
            var vehicles = new[]
            {
                new Vehicles 
                { 
                    Id = 1, 
                    UserId = 1, 
                    LicensePlate = "AB-123-CD", 
                    Brand = "Toyota", 
                    Model = "Corolla",
                    CreatedAt = DateTime.UtcNow
                }
            };
            _context.Vehicles.AddRange(vehicles);

            // Discount Codes
            var discountCodes = new[]
            {
                new DiscountCodes
                {
                    Id = 1,
                    Code = "SAVE10",
                    DiscountValue = 1.00m,
                    DiscountType = "Fixed",
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    MaxUses = 100,
                    UsedCount = 0,
                    CreatedAt = DateTime.UtcNow
                }
            };
            _context.DiscountCodes.AddRange(discountCodes);

            _context.SaveChanges();
        }

        private void SetAuthHeader(int userId, string role)
        {
            // Mock JWT token (in echte situatie zou je een JWT generator gebruiken)
            var token = $"mock_token_user{userId}_role{role}";
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        #region POST /api/payments - CreatePayment Tests

        [Fact]
        public async Task CreatePayment_WithValidData_Returns201Created()
        {
            // Arrange
            SetAuthHeader(1, "User");
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            payment!.LicensePlate.Should().Be("AB-123-CD");
            payment.PaymentStatus.Should().Be("Pending");
            payment.Cost.Should().Be(2.50m);

            // Verify in database
            var dbPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.LicensePlate == "AB-123-CD");
            dbPayment.Should().NotBeNull();
        }

        [Fact]
        public async Task CreatePayment_WithInvalidData_Returns400BadRequest()
        {
            // Arrange
            SetAuthHeader(1, "User");
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "", // Invalid
                Duration = 0 // Invalid
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreatePayment_WithoutAuthentication_Returns401Unauthorized()
        {
            // Arrange - Geen auth header
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreatePayment_WithDiscountCode_AppliesDiscount()
        {
            // Arrange
            SetAuthHeader(1, "User");
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                DiscountCode = "SAVE10"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            payment!.Cost.Should().BeLessThan(2.50m); // Discount applied
        }

        [Fact]
        public async Task CreatePayment_WithUnauthorizedVehicle_Returns403Forbidden()
        {
            // Arrange
            SetAuthHeader(1, "User");
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "WRONG-PLATE",
                Duration = 60
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region PATCH /api/payments/{id}/status - UpdatePaymentStatus Tests

        [Fact]
        public async Task UpdatePaymentStatus_AsAdmin_Returns200Ok()
        {
            // Arrange - Create payment eerst
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Pending",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(2, "Admin");
            var request = new UpdatePaymentStatusDto { NewStatus = "Paid" };

            // Act
            var response = await _client.PatchAsJsonAsync($"/api/payments/{payment.Id}/status", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<PaymentDto>();
            result.Should().NotBeNull();
            result!.PaymentStatus.Should().Be("Paid");
        }

        [Fact]
        public async Task UpdatePaymentStatus_AsUser_Returns403Forbidden()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Pending",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");
            var request = new UpdatePaymentStatusDto { NewStatus = "Paid" };

            // Act
            var response = await _client.PatchAsJsonAsync($"/api/payments/{payment.Id}/status", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdatePaymentStatus_WithInvalidStatus_Returns400BadRequest()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Pending",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(2, "Admin");
            var request = new UpdatePaymentStatusDto { NewStatus = "InvalidStatus" };

            // Act
            var response = await _client.PatchAsJsonAsync($"/api/payments/{payment.Id}/status", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region GET /api/payments/{id} - GetPayment Tests

        [Fact]
        public async Task GetPayment_WithExistingId_Returns200Ok()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync($"/api/payments/{payment.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<PaymentDto>();
            result.Should().NotBeNull();
            result!.Id.Should().Be(payment.Id);
            result.LicensePlate.Should().Be("AB-123-CD");
        }

        [Fact]
        public async Task GetPayment_WithNonExistentId_Returns404NotFound()
        {
            // Arrange
            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync("/api/payments/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region GET /api/payments/{id}/status - GetPaymentStatus Tests

        [Fact]
        public async Task GetPaymentStatus_AsOwner_Returns200Ok()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync($"/api/payments/{payment.Id}/status");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            result.GetProperty("status").GetString().Should().Be("Paid");
        }

        [Fact]
        public async Task GetPaymentStatus_AsOtherUser_Returns403Forbidden()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 2, // Different user
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User"); // User 1 trying to access User 2's payment

            // Act
            var response = await _client.GetAsync($"/api/payments/{payment.Id}/status");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region GET /api/payments/user - GetUserPayments Tests

        [Fact]
        public async Task GetUserPayments_ForOwnPayments_Returns200Ok()
        {
            // Arrange
            var payments = new[]
            {
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 60,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow.AddDays(-2),
                    EndTime = DateTime.UtcNow.AddDays(-2).AddMinutes(60),
                    Cost = 2.50m,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    ModifiedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 120,
                    PaymentStatus = "Pending",
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(120),
                    Cost = 5.00m,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    ModifiedAt = DateTime.UtcNow.AddDays(-1)
                }
            };
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync("/api/payments/user");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<List<PaymentDto>>();
            result.Should().NotBeNull();
            result!.Should().HaveCount(2);
            result.All(p => p.LicensePlate == "AB-123-CD").Should().BeTrue();
        }

        [Fact]
        public async Task GetUserPayments_AsAdminForOtherUser_Returns200Ok()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(2, "Admin");

            // Act
            var response = await _client.GetAsync("/api/payments/user?userId=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<List<PaymentDto>>();
            result.Should().NotBeNull();
            result!.Should().HaveCount(1);
        }

        #endregion

        #region POST /api/payments/{id}/refund - RefundPayment Tests

        [Fact]
        public async Task RefundPayment_AsAdmin_Returns200Ok()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(2, "Admin");

            // Act
            var response = await _client.PostAsync($"/api/payments/{payment.Id}/refund", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<PaymentDto>();
            result.Should().NotBeNull();
            result!.PaymentStatus.Should().Be("Refund");
            result.Cost.Should().Be(-2.50m);

            // Verify original payment marked as Refunded
            var originalPayment = await _context.Payments.FindAsync(payment.Id);
            originalPayment!.PaymentStatus.Should().Be("Refunded");
        }

        [Fact]
        public async Task RefundPayment_AsUser_Returns403Forbidden()
        {
            // Arrange
            var payment = new Payments
            {
                UserId = 1,
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60,
                PaymentStatus = "Paid",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(60),
                Cost = 2.50m,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.PostAsync($"/api/payments/{payment.Id}/refund", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region GET /api/payments/history - GetPaymentHistory Tests

        [Fact]
        public async Task GetPaymentHistory_AsUser_ReturnsOwnPayments()
        {
            // Arrange
            var payments = new[]
            {
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 60,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(60),
                    Cost = 2.50m,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    ModifiedAt = DateTime.UtcNow.AddDays(-1)
                }
            };
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync("/api/payments/history");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<List<PaymentDto>>();
            result.Should().NotBeNull();
            result!.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetPaymentHistory_AsAdmin_ReturnsAllPayments()
        {
            // Arrange
            var payments = new[]
            {
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 60,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(60),
                    Cost = 2.50m,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new Payments
                {
                    UserId = 2,
                    ParkingLotId = 1,
                    LicensePlate = "XY-789-ZZ",
                    Duration = 120,
                    PaymentStatus = "Pending",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(120),
                    Cost = 5.00m,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            };
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            SetAuthHeader(2, "Admin");

            // Act
            var response = await _client.GetAsync("/api/payments/history");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<List<PaymentDto>>();
            result.Should().NotBeNull();
            result!.Should().HaveCount(2);
        }

        #endregion

        #region GET /api/payments/total - GetUserTotal Tests

        [Fact]
        public async Task GetUserTotal_Returns200Ok()
        {
            // Arrange
            var payments = new[]
            {
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 60,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(60),
                    Cost = 2.50m,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new Payments
                {
                    UserId = 1,
                    ParkingLotId = 1,
                    LicensePlate = "AB-123-CD",
                    Duration = 120,
                    PaymentStatus = "Paid",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddMinutes(120),
                    Cost = 5.00m,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            };
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            SetAuthHeader(1, "User");

            // Act
            var response = await _client.GetAsync("/api/payments/total");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            result.GetProperty("userId").GetInt32().Should().Be(1);
            result.GetProperty("transactionCount").GetInt32().Should().Be(2);
            result.GetProperty("total").GetDecimal().Should().Be(7.50m);
        }

        #endregion

        #region Complete Flow Tests

        [Fact]
        public async Task CompletePaymentFlow_CreateUpdateRefund_Success()
        {
            // Step 1: Create payment
            SetAuthHeader(1, "User");
            var createRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 60
            };

            var createResponse = await _client.PostAsJsonAsync("/api/payments", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            var paymentId = payment!.Id;

            // Step 2: Update status to Paid (as Admin)
            SetAuthHeader(2, "Admin");
            var updateRequest = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            var updateResponse = await _client.PatchAsJsonAsync($"/api/payments/{paymentId}/status", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 3: Refund payment (as Admin)
            var refundResponse = await _client.PostAsync($"/api/payments/{paymentId}/refund", null);
            refundResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var refund = await refundResponse.Content.ReadFromJsonAsync<PaymentDto>();
            refund.Should().NotBeNull();
            refund!.PaymentStatus.Should().Be("Refund");
            refund.Cost.Should().BeLessThan(0);
        }

        [Fact]
        public async Task CompletePaymentFlow_WithDiscountCode_Success()
        {
            // Arrange
            SetAuthHeader(1, "User");
            var request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AB-123-CD",
                Duration = 120,
                DiscountCode = "SAVE10"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/payments", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            payment!.Cost.Should().BeLessThan(5.00m); // Original would be 5.00 (2 hours * 2.50)
        }

        #endregion
    }
}