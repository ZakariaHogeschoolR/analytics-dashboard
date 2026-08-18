using Xunit;
using System;
using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Services;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using System.Threading.Tasks;


namespace MobyParkApi.Tests
{
    public class LoginTests
    {
        private ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Login_Succeeds_WhenPasswordCorrect()
        {
            var ctx = CreateContext();
            var service = new UsersService(ctx);

            var hashed = BCrypt.Net.BCrypt.HashPassword("Test123!");
            ctx.Users.Add(new Users { Username = "user", Password = hashed });
            await ctx.SaveChangesAsync();

            var result = await service.LoginAsync(new LoginUserDto
            {
                Username = "user",
                Password = "Test123!"
            });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Login_Fails_WhenPasswordIncorrect()
        {
            var ctx = CreateContext();
            var service = new UsersService(ctx);

            var hashed = BCrypt.Net.BCrypt.HashPassword("Test123!");
            ctx.Users.Add(new Users { Username = "user", Password = hashed });
            await ctx.SaveChangesAsync();

            var result = await service.LoginAsync(new LoginUserDto
            {
                Username = "user",
                Password = "WrongPass!"
            });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_Fails_WhenUserDoesNotExist()
        {
            var ctx = CreateContext();
            var service = new UsersService(ctx);

            var result = await service.LoginAsync(new LoginUserDto
            {
                Username = "ghost",
                Password = "Anything123!"
            });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_Fails_WhenMD5HashStored()
        {
            var ctx = CreateContext();
            var service = new UsersService(ctx);

            // Simuleer oude MD5-hash gebruiker
            var md5 = "cc03e747a6afbbcbf8be7668acfebee5"; // MD5(Test123!)
            ctx.Users.Add(new Users { Username = "olduser", Password = md5 });
            await ctx.SaveChangesAsync();

            // Probeer in te loggen met normaal wachtwoord
            var result = await service.LoginAsync(new LoginUserDto
            {
                Username = "olduser",
                Password = "Test123!"
            });

            // Verwacht: login wordt geweigerd
            Assert.Null(result);
        }
    }
}
