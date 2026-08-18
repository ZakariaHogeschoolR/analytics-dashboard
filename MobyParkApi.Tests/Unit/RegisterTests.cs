using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MobyParkApi.Data;
using MobyParkApi.Services;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Tests.UsersServiceTests
{
    public class RegisterTests
    {
        private static ApplicationDbContext MakeDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private static UsersService MakeService(ApplicationDbContext db)
            => new UsersService(db);

        private static RegisterUserDto ValidDto() => new RegisterUserDto
        {
            Name = "Jan Jansen",
            Username = "jan12345",
            Password = "SterkPass1!",
            Email = "jan@example.com",
            PhoneNumber = "+31612345678",
            BirthYear = 1998
        };

        [Fact]
        public async Task Register_Succeeds_WhenValidData()
        {
            // arrange
            using var db = MakeDb(nameof(Register_Succeeds_WhenValidData));
            var sut = MakeService(db);
            var dto = ValidDto();

            // act
            var result = await sut.RegisterAsync(dto);

            // assert
            Assert.True(result.Success);
            var saved = await db.Users.SingleAsync(u => u.Username == dto.Username);
            Assert.Equal(dto.Email, saved.Email);
            Assert.Equal(dto.PhoneNumber, saved.Phone_Number);   // let op: underscore in DB-model
            Assert.Equal(dto.BirthYear, saved.Birth_Year);
            Assert.Equal("User", saved.Role);
            Assert.True(saved.Active);
        }

        [Fact]
        public async Task Register_Fails_WhenEmailExists()
        {
            using var db = MakeDb(nameof(Register_Fails_WhenEmailExists));
            // seed bestaande user met hetzelfde email
            db.Users.Add(new Users
            {
                Name = "Bestaat Al",
                Username = "albestaat",
                Password = "$2a$10$abcdefghijklmnopqrstuv123456789012345678901234567890", // dummy bcrypt
                Email = "dup@example.com",
                Phone_Number = "+31611111111",
                Birth_Year = 1990,
                Role = "User",
                Active = true
            });
            await db.SaveChangesAsync();

            var sut = MakeService(db);
            var dto = ValidDto();
            dto.Email = "dup@example.com"; // duplicate

            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("E-mailadres bestaat al", result.ErrorMessage);
        }

        [Fact]
        public async Task Register_Fails_WhenUsernameExists()
        {
            using var db = MakeDb(nameof(Register_Fails_WhenUsernameExists));
            db.Users.Add(new Users
            {
                Name = "User Met Zelfde Username",
                Username = "jan12345",
                Password = "$2a$10$abcdefghijklmnopqrstuv123456789012345678901234567890",
                Email = "ander@example.com",
                Phone_Number = "+31622222222",
                Birth_Year = 1991,
                Role = "User",
                Active = true
            });
            await db.SaveChangesAsync();

            var sut = MakeService(db);
            var dto = ValidDto(); // heeft Username = "jan12345"

            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("Gebruikersnaam bestaat al", result.ErrorMessage);
        }

        [Fact]
        public async Task Register_Fails_WhenPhoneExists()
        {
            using var db = MakeDb(nameof(Register_Fails_WhenPhoneExists));
            db.Users.Add(new Users
            {
                Name = "Telefoon Bestaat",
                Username = "telefoonuser",
                Password = "$2a$10$abcdefghijklmnopqrstuv123456789012345678901234567890",
                Email = "tel@example.com",
                Phone_Number = "+31612345678", // duplicate phone
                Birth_Year = 1992,
                Role = "User",
                Active = true
            });
            await db.SaveChangesAsync();

            var sut = MakeService(db);
            var dto = ValidDto(); // PhoneNumber = +31612345678

            var result = await sut.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("Telefoonnummer is al geregistreerd", result.ErrorMessage);
        }

        [Fact]
        public async Task Register_StoresPassword_AsBCryptHash()
        {
            using var db = MakeDb(nameof(Register_StoresPassword_AsBCryptHash));
            var sut = MakeService(db);
            var dto = ValidDto();
            dto.Password = "SterkPass1!";

            var result = await sut.RegisterAsync(dto);

            Assert.True(result.Success);
            var saved = await db.Users.SingleAsync(u => u.Username == dto.Username);

            // wachtwoord mag niet plain zijn
            Assert.NotEqual(dto.Password, saved.Password);

            // bcrypt hashes beginnen met $2a/$2b/$2y
            Assert.StartsWith("$2", saved.Password);
        }
    }
}

