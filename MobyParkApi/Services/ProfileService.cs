using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Services;

public interface IProfileService
{
    Task<Users?> GetProfileAsync(int userId);
    Task<Users?> UpdateProfileAsync(int userId, UpdateProfileDto request);
    Task<bool> DeactivateProfileAsync(int userId);
    Task<ReactivateResultDto> ReactivateProfileAsync(ReactivateProfileDto request);
}

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;

    public ProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Users?> GetProfileAsync(int userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<Users?> UpdateProfileAsync(int userId, UpdateProfileDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.Phone_Number = request.PhoneNumber.Trim();

        if (request.BirthYear.HasValue)
            user.Birth_Year = request.BirthYear.Value;

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);

        user.Modified_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeactivateProfileAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        user.Active = false;
        user.Modified_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ReactivateResultDto> ReactivateProfileAsync(ReactivateProfileDto request)
    {
        // Normaliseer username voor lookup
        var normalizedUsername = request.Username.Trim().ToLower();

        // Zoek gebruiker
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);

        // Gebruiker bestaat niet
        if (user == null)
            return new ReactivateResultDto 
            { 
                Success = false, 
                ErrorMessage = "Gebruiker niet gevonden" 
            };

        // Account is al actief (check voor nullable bool)
        if (user.Active == true)
            return new ReactivateResultDto 
            { 
                Success = false, 
                ErrorMessage = "Account is al actief" 
            };

        // Verifieer wachtwoord
        if (string.IsNullOrEmpty(user.Password) || 
            !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            return new ReactivateResultDto 
            { 
                Success = false, 
                ErrorMessage = "Ongeldig wachtwoord" 
            };
        }

        // Check of Modified_At een waarde heeft
        if (!user.Modified_At.HasValue)
        {
            return new ReactivateResultDto 
            { 
                Success = false, 
                ErrorMessage = "Kan deactivatiedatum niet bepalen" 
            };
        }

        // Check 30-dagen limiet
        var daysSinceDeactivation = (DateTime.UtcNow - user.Modified_At.Value).TotalDays;
        if (daysSinceDeactivation > 30)
        {
            return new ReactivateResultDto 
            { 
                Success = false, 
                ErrorMessage = "Reactivatie niet mogelijk. Account is langer dan 30 dagen geleden gedeactiveerd" 
            };
        }

        // Reactiveer account
        user.Active = true;
        user.Modified_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync();

        return new ReactivateResultDto { Success = true };
    }
}