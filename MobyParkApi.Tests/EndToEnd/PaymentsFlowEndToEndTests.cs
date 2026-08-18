using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.E2E
{
    /// <summary>
    /// End-to-End tests voor complete payment workflows
    /// Test complete user journeys van begin tot eind
    /// </summary>
    public class PaymentFlowE2ETests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ApplicationDbContext _context;

        public PaymentFlowE2ETests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("E2ETestDatabase_" + Guid.NewGuid().ToString());
                    });
                });
            });

            _client = _factory.CreateClient();
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
            var users = new[]
            {
                new Users 
                { 
                    Id = 1, 
                    Email = "john@example.com", 
                    Password = "hashed_password",
                    Role = "User", 
                    FirstName = "John", 
                    LastName = "Doe",
                    Created_At = DateTime.UtcNow
                },
                new Users 
                { 
                    Id = 2, 
                    Email = "jane@example.com", 
                    Password = "hashed_password",
                    Role = "User", 
                    FirstName = "Jane", 
                    LastName = "Smith",
                    Created_At = DateTime.UtcNow
                },
                new Users 
                { 
                    Id = 3, 
                    Email = "admin@example.com", 
                    Password = "hashed_password",
                    Role = "Admin", 
                    FirstName = "Admin", 
                    LastName = "User",
                    Created_At = DateTime.UtcNow
                }
            };
            _context.Users.AddRange(users);

            var parkingLots = new[]
            {
                new ParkingLots 
                { 
                    Id = 1, 
                    Name = "City Center Parking", 
                    Location = "Downtown", 
                    Address = "Main Street 100",
                    Capacity = 200, 
                    Reserved = 0, 
                    Tariff = 3.00m, 
                    DayTariff = 25.00m,
                    Coordinates = "52.370216,4.895168"
                },
                new ParkingLots 
                { 
                    Id = 2, 
                    Name = "Airport Parking", 
                    Location = "Airport", 
                    Address = "Airport Road 1",
                    Capacity = 500, 
                    Reserved = 0, 
                    Tariff = 5.00m, 
                    DayTariff = 40.00m,
                    Coordinates = "52.308056,4.764167"
                }
            };
            _context.ParkingLots.AddRange(parkingLots);

            var vehicles = new[]
            {
                new Vehicles 
                { 
                    Id = 1, 
                    UserId = 1, 
                    LicensePlate = "AA-111-BB", 
                    Brand = "Tesla", 
                    Model = "Model 3",
                    CreatedAt = DateTime.UtcNow
                },
                new Vehicles 
                { 
                    Id = 2, 
                    UserId = 1, 
                    LicensePlate = "CC-222-DD", 
                    Brand = "BMW", 
                    Model = "X5",
                    CreatedAt = DateTime.UtcNow
                },
                new Vehicles 
                { 
                    Id = 3, 
                    UserId = 2, 
                    LicensePlate = "EE-333-FF", 
                    Brand = "Audi", 
                    Model = "A4",
                    CreatedAt = DateTime.UtcNow
                }
            };
            _context.Vehicles.AddRange(vehicles);

            var discountCodes = new[]
            {
                new DiscountCodes
                {
                    Id = 1,
                    Code = "WELCOME20",
                    DiscountValue = 2.00m,
                    DiscountType = "Fixed",
                    StartDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    MaxUses = 100,
                    UsedCount = 0,
                    CreatedAt = DateTime.UtcNow
                },
                new DiscountCodes
                {
                    Id = 2,
                    Code = "EXPIRED",
                    DiscountValue = 5.00m,
                    DiscountType = "Fixed",
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow.AddDays(-1), // Expired
                    MaxUses = 50,
                    UsedCount = 0,
                    CreatedAt = DateTime.UtcNow
                }
            };
            _context.DiscountCodes.AddRange(discountCodes);

            _context.SaveChanges();
        }

        private void SetAuthHeader(int userId, string role)
        {
            var token = $"Bearer mock_token_user{userId}_role{role}";
            _client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }

        #region Complete User Journey Tests

        [Fact]
        public async Task CompleteUserJourney_ParkingWithPayment_Success()
        {
            /*
             * SCENARIO: User parkeert zijn auto en betaalt
             * 1. User maakt payment aan voor 2 uur parkeren
             * 2. User controleert payment status
             * 3. User bekijkt zijn payments
             * 4. User bekijkt totaalbedrag
             */

            // STEP 1: User maakt payment aan
            SetAuthHeader(1, "User");
            var createRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 120 // 2 uur
            };

            var createResponse = await _client.PostAsJsonAsync("/api/payments", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            payment!.Cost.Should().Be(6.00m); // 2 uur * €3.00
            var paymentId = payment.Id;

            // STEP 2: User controleert payment status
            var statusResponse = await _client.GetAsync($"/api/payments/{paymentId}/status");
            statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var statusJson = await statusResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            statusJson.GetProperty("status").GetString().Should().Be("Pending");

            // STEP 3: User bekijkt zijn payments
            var paymentsResponse = await _client.GetAsync("/api/payments/user");
            paymentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var payments = await paymentsResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            payments.Should().NotBeNull();
            payments!.Should().HaveCount(1);
            payments[0].Id.Should().Be(paymentId);

            // STEP 4: User bekijkt totaalbedrag
            var totalResponse = await _client.GetAsync("/api/payments/total");
            totalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var totalJson = await totalResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            totalJson.GetProperty("total").GetDecimal().Should().Be(6.00m);
            totalJson.GetProperty("transactionCount").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task CompleteAdminJourney_PaymentManagement_Success()
        {
            /*
             * SCENARIO: Admin beheert betalingen
             * 1. User maakt payment aan
             * 2. Admin update status naar Paid
             * 3. Admin bekijkt payment history
             * 4. Admin bekijkt totaal van specifieke user
             * 5. Admin processeert refund
             */

            // STEP 1: User maakt payment aan
            SetAuthHeader(1, "User");
            var createRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 60
            };

            var createResponse = await _client.PostAsJsonAsync("/api/payments", createRequest);
            var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
            var paymentId = payment!.Id;

            // STEP 2: Admin update status naar Paid
            SetAuthHeader(3, "Admin");
            var updateRequest = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            var updateResponse = await _client.PatchAsJsonAsync(
                $"/api/payments/{paymentId}/status", 
                updateRequest
            );
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // STEP 3: Admin bekijkt payment history (alle users)
            var historyResponse = await _client.GetAsync("/api/payments/history");
            historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var history = await historyResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            history.Should().NotBeNull();
            history!.Should().HaveCountGreaterOrEqualTo(1);

            // STEP 4: Admin bekijkt totaal van User 1
            var totalResponse = await _client.GetAsync("/api/payments/admin/total?userId=1");
            totalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var totalJson = await totalResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            totalJson.GetProperty("userId").GetInt32().Should().Be(1);
            totalJson.GetProperty("total").GetDecimal().Should().BeGreaterThan(0);

            // STEP 5: Admin processeert refund
            var refundResponse = await _client.PostAsync($"/api/payments/{paymentId}/refund", null);
            refundResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var refund = await refundResponse.Content.ReadFromJsonAsync<PaymentDto>();
            refund.Should().NotBeNull();
            refund!.PaymentStatus.Should().Be("Refund");
            refund.Cost.Should().BeLessThan(0); // Negatieve waarde voor refund
        }

        [Fact]
        public async Task CompleteUserJourney_WithDiscountCode_Success()
        {
            /*
             * SCENARIO: User gebruikt kortingscode
             * 1. User maakt payment met kortingscode
             * 2. Verificatie dat korting is toegepast
             * 3. Admin update naar Paid
             * 4. User controleert totaalbedrag (met korting)
             */

            // STEP 1: User maakt payment met kortingscode
            SetAuthHeader(1, "User");
            var createRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 120, // 2 uur = €6.00
                DiscountCode = "WELCOME20" // -€2.00
            };

            var createResponse = await _client.PostAsJsonAsync("/api/payments", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();
            payment!.Cost.Should().BeLessThan(6.00m); // Korting toegepast
            var paymentId = payment.Id;

            // STEP 2: Verificatie in database
            var dbPayment = await _context.Payments.FindAsync(paymentId);
            dbPayment.Should().NotBeNull();
            dbPayment!.Discount.Should().BeGreaterThan(0);
            dbPayment.DiscountCodeId.Should().NotBeNull();

            // STEP 3: Admin update naar Paid
            SetAuthHeader(3, "Admin");
            var updateRequest = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            var updateResponse = await _client.PatchAsJsonAsync(
                $"/api/payments/{paymentId}/status", 
                updateRequest
            );
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // STEP 4: User controleert totaalbedrag
            SetAuthHeader(1, "User");
            var totalResponse = await _client.GetAsync("/api/payments/total");
            totalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var totalJson = await totalResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            totalJson.GetProperty("total").GetDecimal().Should().BeLessThan(6.00m);
        }

        [Fact]
        public async Task MultipleUsersJourney_DifferentPayments_Success()
        {
            /*
             * SCENARIO: Meerdere users maken payments
             * 1. User 1 maakt payment
             * 2. User 2 maakt payment
             * 3. User 1 kan alleen zijn eigen payments zien
             * 4. User 2 kan alleen zijn eigen payments zien
             * 5. Admin kan alle payments zien
             */

            // STEP 1: User 1 maakt payment
            SetAuthHeader(1, "User");
            var user1Request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 60
            };
            var user1Response = await _client.PostAsJsonAsync("/api/payments", user1Request);
            user1Response.StatusCode.Should().Be(HttpStatusCode.Created);
            var user1Payment = await user1Response.Content.ReadFromJsonAsync<PaymentDto>();

            // STEP 2: User 2 maakt payment
            SetAuthHeader(2, "User");
            var user2Request = new CreatedPaymentDto
            {
                ParkingLotId = 2,
                LicensePlate = "EE-333-FF",
                Duration = 120
            };
            var user2Response = await _client.PostAsJsonAsync("/api/payments", user2Request);
            user2Response.StatusCode.Should().Be(HttpStatusCode.Created);
            var user2Payment = await user2Response.Content.ReadFromJsonAsync<PaymentDto>();

            // STEP 3: User 1 ziet alleen zijn eigen payments
            SetAuthHeader(1, "User");
            var user1PaymentsResponse = await _client.GetAsync("/api/payments/user");
            var user1Payments = await user1PaymentsResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            user1Payments.Should().HaveCount(1);
            user1Payments![0].LicensePlate.Should().Be("AA-111-BB");

            // STEP 4: User 2 ziet alleen zijn eigen payments
            SetAuthHeader(2, "User");
            var user2PaymentsResponse = await _client.GetAsync("/api/payments/user");
            var user2Payments = await user2PaymentsResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            user2Payments.Should().HaveCount(1);
            user2Payments![0].LicensePlate.Should().Be("EE-333-FF");

            // STEP 5: Admin ziet alle payments
            SetAuthHeader(3, "Admin");
            var allPaymentsResponse = await _client.GetAsync("/api/payments/history");
            var allPayments = await allPaymentsResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            allPayments.Should().HaveCount(2);
        }

        [Fact]
        public async Task ErrorRecoveryJourney_InvalidOperations_HandledGracefully()
        {
            /*
             * SCENARIO: User probeert ongeldige operaties
             * 1. User probeert payment te maken met verkeerd kenteken → 403
             * 2. User probeert payment van andere user te bekijken → 403
             * 3. User probeert status te updaten → 403
             * 4. User maakt geldige payment
             * 5. Admin update status succesvol
             */

            // STEP 1: Verkeerd kenteken
            SetAuthHeader(1, "User");
            var invalidRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "EE-333-FF", // Van User 2
                Duration = 60
            };
            var invalidResponse = await _client.PostAsJsonAsync("/api/payments", invalidRequest);
            invalidResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // STEP 2: Maak eerst een payment voor User 2
            SetAuthHeader(2, "User");
            var user2Request = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "EE-333-FF",
                Duration = 60
            };
            var user2Response = await _client.PostAsJsonAsync("/api/payments", user2Request);
            var user2Payment = await user2Response.Content.ReadFromJsonAsync<PaymentDto>();
            var user2PaymentId = user2Payment!.Id;

            // User 1 probeert status van User 2 te bekijken
            SetAuthHeader(1, "User");
            var statusResponse = await _client.GetAsync($"/api/payments/{user2PaymentId}/status");
            statusResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // STEP 3: User probeert status te updaten
            var updateRequest = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            var updateResponse = await _client.PatchAsJsonAsync(
                $"/api/payments/{user2PaymentId}/status", 
                updateRequest
            );
            updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // STEP 4: User maakt geldige payment
            SetAuthHeader(1, "User");
            var validRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 60
            };
            var validResponse = await _client.PostAsJsonAsync("/api/payments", validRequest);
            validResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var validPayment = await validResponse.Content.ReadFromJsonAsync<PaymentDto>();

            // STEP 5: Admin update status succesvol
            SetAuthHeader(3, "Admin");
            var adminUpdateResponse = await _client.PatchAsJsonAsync(
                $"/api/payments/{validPayment!.Id}/status",
                new UpdatePaymentStatusDto { NewStatus = "Paid" }
            );
            adminUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DailyParkingJourney_MultiplePayments_Success()
        {
            /*
             * SCENARIO: User parkeert meerdere keren op één dag
             * 1. Ochtend: 2 uur parkeren
             * 2. Middag: 3 uur parkeren
             * 3. Avond: 1 uur parkeren
             * 4. Bekijk dagelijkse geschiedenis
             * 5. Bekijk totaalbedrag
             */

            SetAuthHeader(1, "User");

            // STEP 1: Ochtend parkeren
            var morningRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 120 // 2 uur
            };
            var morningResponse = await _client.PostAsJsonAsync("/api/payments", morningRequest);
            morningResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // STEP 2: Middag parkeren
            var afternoonRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 180 // 3 uur
            };
            var afternoonResponse = await _client.PostAsJsonAsync("/api/payments", afternoonRequest);
            afternoonResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // STEP 3: Avond parkeren met andere auto
            var eveningRequest = new CreatedPaymentDto
            {
                ParkingLotId = 2,
                LicensePlate = "CC-222-DD",
                Duration = 60 // 1 uur
            };
            var eveningResponse = await _client.PostAsJsonAsync("/api/payments", eveningRequest);
            eveningResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // STEP 4: Bekijk geschiedenis
            var historyResponse = await _client.GetAsync("/api/payments/history");
            historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var history = await historyResponse.Content.ReadFromJsonAsync<List<PaymentDto>>();
            history.Should().HaveCount(3);

            // STEP 5: Bekijk totaalbedrag
            var totalResponse = await _client.GetAsync("/api/payments/total");
            var totalJson = await totalResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            
            // 2u*€3 + 3u*€3 + 1u*€5 = €6 + €9 + €5 = €20
            totalJson.GetProperty("total").GetDecimal().Should().Be(20.00m);
            totalJson.GetProperty("transactionCount").GetInt32().Should().Be(3);
        }

        [Fact]
        public async Task RefundJourney_CompleteRefundProcess_Success()
        {
            /*
             * SCENARIO: Complete refund proces
             * 1. User maakt payment
             * 2. Admin update naar Paid
             * 3. User vraagt refund aan (alleen admin kan)
             * 4. Admin processeert refund
             * 5. Verificatie: originele payment = Refunded
             * 6. Verificatie: nieuwe refund entry bestaat
             * 7. Totaalbedrag reflecteert refund
             */

            // STEP 1: User maakt payment
            SetAuthHeader(1, "User");
            var createRequest = new CreatedPaymentDto
            {
                ParkingLotId = 1,
                LicensePlate = "AA-111-BB",
                Duration = 120
            };
            var createResponse = await _client.PostAsJsonAsync("/api/payments", createRequest);
            var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
            var paymentId = payment!.Id;
            var originalCost = payment.Cost;

            // STEP 2: Admin update naar Paid
            SetAuthHeader(3, "Admin");
            var updateRequest = new UpdatePaymentStatusDto { NewStatus = "Paid" };
            await _client.PatchAsJsonAsync($"/api/payments/{paymentId}/status", updateRequest);

            // STEP 3: User probeert refund (moet falen)
            SetAuthHeader(1, "User");
            var userRefundResponse = await _client.PostAsync($"/api/payments/{paymentId}/refund", null);
            userRefundResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // STEP 4: Admin processeert refund
            SetAuthHeader(3, "Admin");
            var refundResponse = await _client.PostAsync($"/api/payments/{paymentId}/refund", null);
            refundResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var refund = await refundResponse.Content.ReadFromJsonAsync<PaymentDto>();
            refund.Should().NotBeNull();
            refund!.Cost.Should().Be(-originalCost);

            // STEP 5: Verificatie in database - originele payment
            var originalPayment = await _context.Payments.FindAsync(paymentId);
            originalPayment!.PaymentStatus.Should().Be("Refunded");

            // STEP 6: Verificatie - refund entry
            var refundEntry = await _context.Payments
                .Where(p => p.PaymentStatus == "Refund" && p.UserId == 1)
                .FirstOrDefaultAsync();
            refundEntry.Should().NotBeNull();
            refundEntry!.Cost.Should().Be(-originalCost);

            // STEP 7: Totaalbedrag check
            SetAuthHeader(1, "User");
            var totalResponse = await _client.GetAsync("/api/payments/total");
            var totalJson = await totalResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            
            // Original cost - refund cost = 0
            totalJson.GetProperty("total").GetDecimal().Should().Be(0.00m);
        }

        #endregion
    }
}