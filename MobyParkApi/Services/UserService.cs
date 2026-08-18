using BCrypt.Net;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using Microsoft.EntityFrameworkCore;

namespace MobyParkApi.Services
{
    public interface IUsersService
    {
        Task<RegisterResultDto> RegisterAsync(RegisterUserDto request);
        Task<Users?> LoginAsync(LoginUserDto request);
        
    }

    public class UsersService : IUsersService
    {
        private readonly ApplicationDbContext _context;

        public UsersService(ApplicationDbContext context)
        {
            _context = context;
        }

         /// <summary>
        /// Registreert een nieuwe gebruiker met beveiligingschecks
        /// </summary>
        public async Task<RegisterResultDto> RegisterAsync(RegisterUserDto request)
        {
            // zorgen dat het lowercase is zodat het niet fout kan gaan met controleren
            var normalizedEmail = request.Email.Trim().ToLower();
            var normalizedUsername = request.Username.Trim().ToLower();
            var normalizedPhone = request.PhoneNumber.Trim();

            // checked of de email, username en telefoonnummer al bestaan
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return new RegisterResultDto { Success = false, ErrorMessage = "E-mailadres bestaat al" };

             if (await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername))
                return new RegisterResultDto { Success = false, ErrorMessage = "Gebruikersnaam bestaat al" };

             if (await _context.Users.AnyAsync(u => u.Phone_Number.ToLower() == normalizedPhone.ToLower()))
                return new RegisterResultDto { Success = false, ErrorMessage = "Telefoonnummer is al geregistreerd" };

            // de email, username en telefoonnummer met lowercase/trim opslaan
            var user = new Users
            {
                Name = request.Name,
                Username = normalizedUsername,
                Email = normalizedEmail,
                Phone_Number = normalizedPhone,
                Birth_Year = request.BirthYear,
                Role = "User",
                Active = true,
                Created_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                Modified_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new RegisterResultDto { Success = true };
        }

        /// <summary>
        /// Logt een gebruiker in met beveiligingschecks
        /// </summary>
        public async Task<Users?> LoginAsync(LoginUserDto request)
        {   
            // username trim + lower voor fouten met hoofdletter in toekomst
            var normalizedUsername = request.Username.Trim().ToLower();

            // zoekt gebruiker met aangemaakte username die trim/lower is
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);

            // Gebruiker bestaat niet
            if (user == null)
                return null;

            // Als active op false staat niet laten inloggen
            if (user.Active == false)
                throw new UnauthorizedAccessException("Account is gedeactiveerd");

            // haal gehashte wachtwoord op
            var stored = user.Password ?? "";
            bool validPassword = false;

            // checked of het wachtwoord BCrypt is
            if (stored.StartsWith("$2a$") || stored.StartsWith("$2b$") || stored.StartsWith("$2y$"))
            {
                validPassword = BCrypt.Net.BCrypt.Verify(request.Password, stored);
            }
            else
            {
                return null;
            }

            // Wachtwood is niet correct
            if (!validPassword)
                return null;

            // ALles klopt return gebruiker
            return user;
        }
    }
}

//  public async Task<Users?> LoginAsync(LoginUserDto request)
//         {
//             var user = await _context.Users
//                 .FirstOrDefaultAsync(u => u.Username == request.Username);

//             if (user == null)
//                 return null;

//             bool validPassword = false;
//             var stored = user.Password ?? "";

//             // 1) If stored looks like bcrypt ($2a$ / $2b$ / $2y$ ...)
//             if (stored.StartsWith("$2"))
//             {
//                 // Expect request.Password to be plain
//                 validPassword = BCrypt.Net.BCrypt.Verify(request.Password, stored);
//             }
//             else
//             {
//                 // 2) If client sent exactly the stored hash (i.e. they pasted DB-hash as "password")
//                 // TODO: REMOVE THIS logic before production deployment
//                 if (!string.IsNullOrEmpty(request.Password) &&
//                     string.Equals(request.Password.Trim(), stored.Trim(), StringComparison.OrdinalIgnoreCase))
//                 {
//                     // accept — client submitted the same hash that's stored
//                     validPassword = true;

//                     // OPTIONAL: re-hash to bcrypt so DB no longer holds legacy hash
//                     try
//                     {
//                         user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
//                         await _context.SaveChangesAsync();
//                     }
//                     catch { /* swallow save errors or log */ }
//                 }
//                 else
//                 {
//                     // 3) Treat request.Password as plain text: compute MD5 and compare
//                     if (VerifyMd5Password(request.Password, stored))
//                     {
//                         // migrate: store bcrypt hash instead
//                         user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
//                         await _context.SaveChangesAsync();
//                         validPassword = true;
//                     }
//                 }
//             }

//             if (!validPassword)
//                 return null;

//             return user;
//         }

//         // helper
//         private bool VerifyMd5Password(string plain, string md5Hash)
//         {
//             if (string.IsNullOrEmpty(plain) || string.IsNullOrEmpty(md5Hash))
//                 return false;

//             using var md5 = System.Security.Cryptography.MD5.Create();
//             var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
//             var hashed = Convert.ToHexString(md5.ComputeHash(bytes)).ToLowerInvariant();
//             return string.Equals(hashed, md5Hash.ToLowerInvariant(), StringComparison.Ordinal);
//         }
