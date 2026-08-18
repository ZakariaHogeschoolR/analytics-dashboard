using System.Net;
using System.Net.Http.Json;
using Xunit;
using FluentAssertions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.System
{
    /// <summary>
    /// System tests voor complete deployment met Docker en PostgreSQL
    /// Deze tests gebruiken TestContainers voor echte database en API deployment
    /// </summary>
    public class PaymentSystemTests : IAsyncLifetime
    {
        private IContainer? _postgresContainer;
        private IContainer? _apiContainer;
        private HttpClient? _client;
        private string _connectionString = string.Empty;

        public async Task InitializeAsync()
        {
            // Start PostgreSQL container
            _postgresContainer = new ContainerBuilder()
                .WithImage("postgres:15-alpine")
                .WithEnvironment("POSTGRES_USER", "testuser")
                .WithEnvironment("POSTGRES_PASSWORD", "testpass")
                .WithEnvironment("POSTGRES_DB", "mobypark_test")
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
                .Build();

            await _postgresContainer.StartAsync();

            var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
            _connectionString = $"Host=localhost;Port={postgresPort};Database=mobypark_test;Username=testuser;Password=testpass";

            // Wacht tot database klaar is
            await WaitForDatabaseAsync();

            // Run migrations (in productie zou je dit via API doen)
            await RunMigrationsAsync();

            // Start API container (in productie - voor nu skippen we dit)
            // _apiContainer = await StartApiContainerAsync();
            
            // Voor nu gebruiken we een mock HttpClient
            _client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            
            if (_apiContainer != null)
                await _apiContainer.StopAsync();
            
            if (_postgresContainer != null)
                await _postgresContainer.StopAsync();
        }

        private async Task WaitForDatabaseAsync()
        {
            var maxRetries = 30;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();
                    return;
                }
                catch
                {
                    retryCount++;
                    await Task.Delay(1000);
                }
            }

            throw new Exception("Database did not start in time");
        }

        private async Task RunMigrationsAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create tables (simplified - in productie gebruik je EF migrations)
            var createTablesScript = @"
                CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    email VARCHAR(255) NOT NULL,
                    password VARCHAR(255) NOT NULL,
                    role VARCHAR(50) NOT NULL,
                    first_name VARCHAR(100),
                    last_name VARCHAR(100),
                    created_at TIMESTAMP,
                    modified_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS parking_lots (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    location VARCHAR(255),
                    address VARCHAR(255),
                    capacity INTEGER NOT NULL,
                    reserved INTEGER DEFAULT 0,
                    tariff DECIMAL(18,2) NOT NULL,
                    day_tariff DECIMAL(18,2),
                    coordinates VARCHAR(100),
                    created_at TIMESTAMP,
                    modified_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS vehicles (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL,
                    license_plate VARCHAR(20) NOT NULL,
                    brand VARCHAR(100),
                    model VARCHAR(100),
                    created_at TIMESTAMP NOT NULL,
                    modified_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS payments (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER,
                    parking_lot_id INTEGER NOT NULL,
                    license_plate VARCHAR(20) NOT NULL,
                    duration INTEGER NOT NULL,
                    payment_status VARCHAR(50) NOT NULL,
                    start_time TIMESTAMP NOT NULL,
                    end_time TIMESTAMP NOT NULL,
                    cost DECIMAL(18,2) NOT NULL,
                    discount DECIMAL(18,2) DEFAULT 0,
                    created_at TIMESTAMP NOT NULL,
                    modified_at TIMESTAMP NOT NULL,
                    discount_code_id INTEGER
                );
            ";

            await using var command = new NpgsqlCommand(createTablesScript, connection);
            await command.ExecuteNonQueryAsync();
        }

        #region Database Connection Tests

        [Fact]
        public async Task Database_Connection_IsEstablished()
        {
            // Arrange & Act
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Assert
            connection.State.Should().Be(System.Data.ConnectionState.Open);
        }

        [Fact]
        public async Task Database_Tables_AreCreated()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Act - Check if tables exist
            var checkTablesQuery = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
            ";

            await using var command = new NpgsqlCommand(checkTablesQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            // Assert
            tables.Should().Contain("users");
            tables.Should().Contain("parking_lots");
            tables.Should().Contain("vehicles");
            tables.Should().Contain("payments");
        }

        [Fact]
        public async Task Database_CanInsertAndRetrieveUser()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Act - Insert user
            var insertQuery = @"
                INSERT INTO users (email, password, role, first_name, last_name, created_at)
                VALUES (@email, @password, @role, @firstName, @lastName, @createdAt)
                RETURNING id
            ";

            int userId;
            await using (var insertCommand = new NpgsqlCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("email", "test@example.com");
                insertCommand.Parameters.AddWithValue("password", "hashed_password");
                insertCommand.Parameters.AddWithValue("role", "User");
                insertCommand.Parameters.AddWithValue("firstName", "Test");
                insertCommand.Parameters.AddWithValue("lastName", "User");
                insertCommand.Parameters.AddWithValue("createdAt", DateTime.UtcNow);

                userId = (int)(await insertCommand.ExecuteScalarAsync())!;
            }

            // Retrieve user
            var selectQuery = "SELECT email, role FROM users WHERE id = @id";
            await using var selectCommand = new NpgsqlCommand(selectQuery, connection);
            selectCommand.Parameters.AddWithValue("id", userId);

            await using var reader = await selectCommand.ExecuteReaderAsync();
            await reader.ReadAsync();

            // Assert
            reader.GetString(0).Should().Be("test@example.com");
            reader.GetString(1).Should().Be("User");
        }

        #endregion

        #region Payment Data Integrity Tests

        [Fact]
        public async Task Database_PaymentInsertion_MaintainsDataIntegrity()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Insert required data
            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);
            await InsertTestVehicleAsync(connection);

            // Act - Insert payment
            var insertPaymentQuery = @"
                INSERT INTO payments (
                    user_id, parking_lot_id, license_plate, duration,
                    payment_status, start_time, end_time, cost,
                    created_at, modified_at
                )
                VALUES (
                    1, 1, 'AB-123-CD', 60,
                    'Pending', @startTime, @endTime, 2.50,
                    @createdAt, @modifiedAt
                )
                RETURNING id
            ";

            int paymentId;
            await using (var command = new NpgsqlCommand(insertPaymentQuery, connection))
            {
                var now = DateTime.UtcNow;
                command.Parameters.AddWithValue("startTime", now);
                command.Parameters.AddWithValue("endTime", now.AddMinutes(60));
                command.Parameters.AddWithValue("createdAt", now);
                command.Parameters.AddWithValue("modifiedAt", now);

                paymentId = (int)(await command.ExecuteScalarAsync())!;
            }

            // Assert - Retrieve and verify
            var selectQuery = @"
                SELECT user_id, parking_lot_id, license_plate, cost, payment_status
                FROM payments WHERE id = @id
            ";

            await using var selectCommand = new NpgsqlCommand(selectQuery, connection);
            selectCommand.Parameters.AddWithValue("id", paymentId);

            await using var reader = await selectCommand.ExecuteReaderAsync();
            await reader.ReadAsync();

            reader.GetInt32(0).Should().Be(1); // user_id
            reader.GetInt32(1).Should().Be(1); // parking_lot_id
            reader.GetString(2).Should().Be("AB-123-CD");
            reader.GetDecimal(3).Should().Be(2.50m);
            reader.GetString(4).Should().Be("Pending");
        }

        [Fact]
        public async Task Database_PaymentUpdate_UpdatesModifiedAt()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);
            await InsertTestVehicleAsync(connection);

            // Insert payment
            var paymentId = await InsertTestPaymentAsync(connection);
            var originalModifiedAt = DateTime.UtcNow;
            
            await Task.Delay(1000); // Ensure time difference

            // Act - Update payment
            var updateQuery = @"
                UPDATE payments 
                SET payment_status = 'Paid', modified_at = @modifiedAt
                WHERE id = @id
            ";

            await using var updateCommand = new NpgsqlCommand(updateQuery, connection);
            updateCommand.Parameters.AddWithValue("modifiedAt", DateTime.UtcNow);
            updateCommand.Parameters.AddWithValue("id", paymentId);
            await updateCommand.ExecuteNonQueryAsync();

            // Assert - Check modified_at was updated
            var selectQuery = "SELECT payment_status, modified_at FROM payments WHERE id = @id";
            await using var selectCommand = new NpgsqlCommand(selectQuery, connection);
            selectCommand.Parameters.AddWithValue("id", paymentId);

            await using var reader = await selectCommand.ExecuteReaderAsync();
            await reader.ReadAsync();

            reader.GetString(0).Should().Be("Paid");
            reader.GetDateTime(1).Should().BeAfter(originalModifiedAt);
        }

        #endregion

        #region Transaction Tests

        [Fact]
        public async Task Database_Transaction_RollbackOnError()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);

            // Act - Start transaction
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Insert valid payment
                var insertQuery1 = @"
                    INSERT INTO payments (
                        user_id, parking_lot_id, license_plate, duration,
                        payment_status, start_time, end_time, cost,
                        created_at, modified_at
                    )
                    VALUES (1, 1, 'AB-123-CD', 60, 'Pending', @time, @time, 2.50, @time, @time)
                ";

                await using (var command = new NpgsqlCommand(insertQuery1, connection, transaction))
                {
                    command.Parameters.AddWithValue("time", DateTime.UtcNow);
                    await command.ExecuteNonQueryAsync();
                }

                // This should fail - invalid parking_lot_id
                var insertQuery2 = @"
                    INSERT INTO payments (
                        user_id, parking_lot_id, license_plate, duration,
                        payment_status, start_time, end_time, cost,
                        created_at, modified_at
                    )
                    VALUES (1, 999, 'XY-789-ZZ', 60, 'Pending', @time, @time, 2.50, @time, @time)
                ";

                await using var command2 = new NpgsqlCommand(insertQuery2, connection, transaction);
                command2.Parameters.AddWithValue("time", DateTime.UtcNow);
                await command2.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
            }

            // Assert - No payments should exist (rollback happened)
            var countQuery = "SELECT COUNT(*) FROM payments";
            await using var countCommand = new NpgsqlCommand(countQuery, connection);
            var count = (long)(await countCommand.ExecuteScalarAsync())!;

            count.Should().Be(0);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Database_BulkInsert_CompletesInReasonableTime()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);
            await InsertTestVehicleAsync(connection);

            var startTime = DateTime.UtcNow;

            // Act - Insert 100 payments
            for (int i = 0; i < 100; i++)
            {
                await InsertTestPaymentAsync(connection, licensePlate: $"AB-{i:D3}-CD");
            }

            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalSeconds;

            // Assert - Should complete in under 5 seconds
            duration.Should().BeLessThan(5);

            // Verify count
            var countQuery = "SELECT COUNT(*) FROM payments";
            await using var countCommand = new NpgsqlCommand(countQuery, connection);
            var count = (long)(await countCommand.ExecuteScalarAsync())!;

            count.Should().Be(100);
        }

        [Fact]
        public async Task Database_Query_IndexedSearchIsFast()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);
            await InsertTestVehicleAsync(connection);

            // Insert 1000 payments
            for (int i = 0; i < 1000; i++)
            {
                await InsertTestPaymentAsync(connection, licensePlate: $"AB-{i:D4}-CD");
            }

            // Act - Search for specific payment
            var startTime = DateTime.UtcNow;

            var searchQuery = "SELECT * FROM payments WHERE license_plate = @plate";
            await using var command = new NpgsqlCommand(searchQuery, connection);
            command.Parameters.AddWithValue("plate", "AB-0500-CD");

            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;

            // Assert - Should complete in under 100ms (without index might be slower)
            duration.Should().BeLessThan(100);
        }

        #endregion

        #region Connection Pool Tests

        [Fact]
        public async Task Database_ConnectionPool_HandlesMultipleConnections()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act - Open 20 concurrent connections
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();
                    
                    // Simulate some work
                    var query = "SELECT 1";
                    await using var command = new NpgsqlCommand(query, connection);
                    await command.ExecuteScalarAsync();
                    
                    await Task.Delay(100);
                }));
            }

            // Assert - All connections should complete successfully
            await Task.WhenAll(tasks);
        }

        #endregion

        #region Environment Configuration Tests

        [Fact]
        public void Environment_ConnectionString_IsConfigured()
        {
            // Assert
            _connectionString.Should().NotBeNullOrEmpty();
            _connectionString.Should().Contain("Database=mobypark_test");
            _connectionString.Should().Contain("Username=testuser");
        }

        [Fact]
        public async Task Environment_DatabaseVersion_IsCorrect()
        {
            // Arrange
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Act
            var versionQuery = "SELECT version()";
            await using var command = new NpgsqlCommand(versionQuery, connection);
            var version = (string)(await command.ExecuteScalarAsync())!;

            // Assert
            version.Should().Contain("PostgreSQL");
            version.Should().Contain("15"); // PostgreSQL 15
        }

        #endregion

        #region Helper Methods

        private async Task InsertTestUserAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO users (id, email, password, role, first_name, last_name, created_at)
                VALUES (1, 'test@example.com', 'hashed_password', 'User', 'Test', 'User', @createdAt)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync();
        }

        private async Task InsertTestParkingLotAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO parking_lots (id, name, location, address, capacity, reserved, tariff, day_tariff)
                VALUES (1, 'Test Parking', 'Center', 'Main St 1', 100, 0, 2.50, 20.00)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task InsertTestVehicleAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO vehicles (id, user_id, license_plate, brand, model, created_at)
                VALUES (1, 1, 'AB-123-CD', 'Tesla', 'Model 3', @createdAt)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<int> InsertTestPaymentAsync(
            NpgsqlConnection connection, 
            string licensePlate = "AB-123-CD")
        {
            var query = @"
                INSERT INTO payments (
                    user_id, parking_lot_id, license_plate, duration,
                    payment_status, start_time, end_time, cost,
                    created_at, modified_at
                )
                VALUES (
                    1, 1, @plate, 60,
                    'Pending', @time, @time, 2.50,
                    @time, @time
                )
                RETURNING id
            ";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("plate", licensePlate);
            command.Parameters.AddWithValue("time", DateTime.UtcNow);

            return (int)(await command.ExecuteScalarAsync())!;
        }

        #endregion
    }
}