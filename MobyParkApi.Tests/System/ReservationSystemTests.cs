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
    public class ReservationSystemTests : IAsyncLifetime
    {
        private IContainer? _postgresContainer;
        private HttpClient? _client;
        private string _connectionString = string.Empty;

        public async Task InitializeAsync()
        {
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

            await WaitForDatabaseAsync();
            await RunMigrationsAsync();

            _client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            if (_postgresContainer != null)
                await _postgresContainer.StopAsync();
        }

        private async Task WaitForDatabaseAsync()
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();
                    return;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
            throw new Exception("Database did not start");
        }

        private async Task RunMigrationsAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var createTablesScript = @"
                CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(255) NOT NULL,
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
                    coordinates VARCHAR(100)
                );

                CREATE TABLE IF NOT EXISTS vehicles (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL,
                    license_plate VARCHAR(20) NOT NULL,
                    brand VARCHAR(100),
                    model VARCHAR(100),
                    created_at TIMESTAMP NOT NULL
                );

                CREATE TABLE IF NOT EXISTS reservations (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER,
                    parking_lot_id INTEGER NOT NULL,
                    vehicle_id INTEGER NOT NULL,
                    start_time TIMESTAMP NOT NULL,
                    end_time TIMESTAMP,
                    status VARCHAR(50) NOT NULL,
                    cost DECIMAL(18,2) NOT NULL,
                    created_at TIMESTAMP NOT NULL,
                    modified_at TIMESTAMP,
                    discount_code_id INTEGER
                );
            ";

            await using var command = new NpgsqlCommand(createTablesScript, connection);
            await command.ExecuteNonQueryAsync();
        }

        [Fact]
        public async Task Database_Connection_IsEstablished()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            connection.State.Should().Be(System.Data.ConnectionState.Open);
        }

        [Fact]
        public async Task Database_Tables_AreCreated()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var checkTablesQuery = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public'
            ";

            await using var command = new NpgsqlCommand(checkTablesQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            tables.Should().Contain("reservations");
            tables.Should().Contain("parking_lots");
            tables.Should().Contain("vehicles");
        }

        [Fact]
        public async Task Database_ReservationInsertion_MaintainsIntegrity()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await InsertTestUserAsync(connection);
            await InsertTestParkingLotAsync(connection);
            await InsertTestVehicleAsync(connection);

            var insertQuery = @"
                INSERT INTO reservations (
                    user_id, parking_lot_id, vehicle_id,
                    start_time, end_time, status, cost, created_at
                )
                VALUES (1, 1, 1, @start, @end, 'Pending', 5.00, @created)
                RETURNING id
            ";

            int reservationId;
            await using (var command = new NpgsqlCommand(insertQuery, connection))
            {
                var now = DateTime.UtcNow;
                command.Parameters.AddWithValue("start", now.AddHours(1));
                command.Parameters.AddWithValue("end", now.AddHours(3));
                command.Parameters.AddWithValue("created", now);

                reservationId = (int)(await command.ExecuteScalarAsync())!;
            }

            var selectQuery = "SELECT status, cost FROM reservations WHERE id = @id";
            await using var selectCommand = new NpgsqlCommand(selectQuery, connection);
            selectCommand.Parameters.AddWithValue("id", reservationId);

            await using var reader = await selectCommand.ExecuteReaderAsync();
            await reader.ReadAsync();

            reader.GetString(0).Should().Be("Pending");
            reader.GetDecimal(1).Should().Be(5.00m);
        }

        private async Task InsertTestUserAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO users (id, username, email, password, role, first_name, last_name, created_at)
                VALUES (1, 'test', 'test@test.com', 'hash', 'User', 'Test', 'User', @created)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("created", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync();
        }

        private async Task InsertTestParkingLotAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO parking_lots (id, name, location, address, capacity, reserved, tariff, day_tariff)
                VALUES (1, 'Test', 'Center', 'Main 1', 10, 0, 2.50, 20.00)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task InsertTestVehicleAsync(NpgsqlConnection connection)
        {
            var query = @"
                INSERT INTO vehicles (id, user_id, license_plate, brand, model, created_at)
                VALUES (1, 1, 'AB-123-CD', 'Tesla', 'Model 3', @created)
                ON CONFLICT (id) DO NOTHING
            ";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("created", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync();
        }
    }
}