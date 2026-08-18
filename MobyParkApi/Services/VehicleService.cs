using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
using Microsoft.Extensions.Logging;
using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using Xunit.Sdk;

namespace MobyParkApi.Controllers;

public class VehiclesService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VehiclesController> _logger;
    private readonly IArchiveService _archiveService;

    public VehiclesService(ApplicationDbContext context, ILogger<VehiclesController> logger, IArchiveService archiveService)
    {
        _context = context;
        _logger = logger;
        _archiveService = archiveService;
    }

    #region GET Endpoints

    /// <summary>
    /// GET /api/vehicles - Haal alle voertuigen op van de ingelogde gebruiker
    /// </summary>
    public async Task<List<Vehicles>> GetMyVehiclesService(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");

        var vehicles = await _context.Vehicles
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("Gebruiker {UserId} haalt {Count} voertuigen op", userId, vehicles.Count);

        return vehicles;
    }

    /// <summary>
    /// GET /api/vehicles/all - Haal alle voertuigen op (ADMIN only)
    /// </summary>
    public async Task<IEnumerable<object>> GetAllVehiclesService()
    {
        // Haal alle voertuigen op
        var vehicles = await _context.Vehicles
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        // Haal alle users op die bij deze voertuigen horen
        var userIds = vehicles.Select(v => v.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        // Map naar response object
        var result = vehicles.Select(v => new
        {
            id = v.Id,
            userId = v.UserId,
            userName = users.ContainsKey(v.UserId) ? users[v.UserId].Username : null,
            licensePlate = v.LicensePlate,
            brand = v.Make, 
            model = v.Model,
            color = v.Color,
            createdAt = v.CreatedAt,
            modifiedAt = v.ModifiedAt
        });

        _logger.LogInformation("Admin haalt alle voertuigen op: {Count} voertuigen", vehicles.Count);

        return result;
    }
    

    /// <summary>
    /// GET /api/vehicles/{id} - Haal een specifiek voertuig op
    /// </summary>
    public async Task<Vehicles> GetVehicleService(int id, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");

        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null)
            throw new KeyNotFoundException($"Voertuig met ID {id} niet gevonden");

        if (vehicle.UserId != userId)
            throw new UnauthorizedAccessException("Access denied");

        _logger.LogInformation("Gebruiker {UserId} haalt voertuig {VehicleId} op", userId, id);

        return vehicle;
    }

    #endregion

    #region POST Endpoints

    /// <summary>
    /// POST /api/vehicles - Maak een nieuw voertuig aan
    /// </summary>
    public async Task<Vehicles> CreateVehicleService(CreateVehicleRequestDto request, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            throw new  UnauthorizedAccessException("Gebruiker niet gevonden in token");

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(request.LicensePlate))
            missingFields.Add("license_plate");
        if (string.IsNullOrWhiteSpace(request.Make))
            missingFields.Add("make");
        if (string.IsNullOrWhiteSpace(request.Model))
            missingFields.Add("model");
        if (string.IsNullOrWhiteSpace(request.Color))
            missingFields.Add("color");
        if (request.Year <= 0)
            missingFields.Add("year");

        if (missingFields.Count > 0)
        {
            var ex = new ArgumentException("Verplichte velden ontbreken of zijn ongeldig");
            ex.Data["Fields"] = missingFields;
            throw ex;
        }

        // Validatie: Nederlands kenteken format
        if (!IsValidDutchLicensePlate(request.LicensePlate))
            throw new ArgumentException($"Ongeldig Nederlands kenteken format: {request.LicensePlate}");

        // Validatie: Check of kenteken al bestaat voor deze gebruiker
        if (await LicensePlateExistsForUser(request.LicensePlate, userId))
            throw new ArgumentException("Kenteken bestaat al voor deze gebruiker");

        var vehicle = new Vehicles
        {
            LicensePlate = request.LicensePlate.Trim().ToUpper(),
            Make = request.Make,
            Model = request.Model,
            Color = request.Color,
            Year = request.Year,
            UserId = userId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Voertuig {VehicleId} aangemaakt door gebruiker {UserId}", vehicle.Id, userId);

        return vehicle;
    }

    #endregion

    #region PATCH Endpoints

    /// <summary>
    /// PATCH /api/vehicles/{id} - Update een voertuig
    /// </summary>
    public async Task<Vehicles> UpdateVehicleService(int id, UpdateVehicleRequestDto request, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");

        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null)
            throw new KeyNotFoundException($"Voertuig met ID {id} niet gevonden");

        if (vehicle.UserId != userId)
            throw new UnauthorizedAccessException("Access denied");

        // Validatie: Als kenteken wordt gewijzigd
        if (!string.IsNullOrWhiteSpace(request.LicensePlate) && 
            request.LicensePlate.ToUpper() != vehicle.LicensePlate.ToUpper())
        {
            // Check format
            if (!IsValidDutchLicensePlate(request.LicensePlate))
                throw new ArgumentException($"Ongeldig Nederlands kenteken format: {request.LicensePlate}");

            // Check of nieuw kenteken al bestaat (bij ander voertuig)
            if (await LicensePlateExistsForUser(request.LicensePlate, userId, id))
                throw new ArgumentException($"Je hebt al een ander voertuig met kenteken {request.LicensePlate}");

            vehicle.LicensePlate = request.LicensePlate.ToUpper();
        }
    
        if (!string.IsNullOrWhiteSpace(request.Make))
            vehicle.Make = request.Make;
    
        if (!string.IsNullOrWhiteSpace(request.Model))
            vehicle.Model = request.Model;
    
        if (!string.IsNullOrWhiteSpace(request.Color))
            vehicle.Color = request.Color;
    
        if (request.Year.HasValue)
            vehicle.Year = request.Year;

        vehicle.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Voertuig {VehicleId} geüpdatet door gebruiker {UserId}", id, userId);

        return vehicle;
    }

    #endregion

    #region DELETE Endpoints

    /// <summary>
    /// DELETE /api/vehicles/{id} - Verwijder een voertuig (archiveert voertuig en gerelateerde reserveringen)
    /// </summary>
    public async Task<string> DeleteVehicleService(int id, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            throw new UnauthorizedAccessException("Gebruiker niet gevonden in token");

        var userRole = user.FindFirstValue(ClaimTypes.Role) ?? "User";
        var username = user.FindFirstValue(ClaimTypes.Name) ?? $"User_{userId}";
        
        // Debug logging
        _logger.LogInformation("DELETE Vehicle {VehicleId} - User: {UserId}, Role: {Role}, Username: {Username}", id, userId, userRole, username);

        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null)
            throw new KeyNotFoundException($"Voertuig met ID {id} niet gevonden");

        // Admin check met logging
        bool isAdmin = IsAdmin(userRole);
        bool isOwner = vehicle.UserId == userId;
        
        _logger.LogInformation("Authorization check - VehicleId: {VehicleId}, Owner: {OwnerId}, CurrentUser: {UserId}, IsAdmin: {IsAdmin}, IsOwner: {IsOwner}",
            id, vehicle.UserId, userId, isAdmin, isOwner);

        if (!isOwner && !isAdmin)
        {
            _logger.LogWarning("DENIED - User {UserId} (Role: {Role}) cannot delete vehicle {VehicleId} owned by {OwnerId}",
                userId, userRole, id, vehicle.UserId);
            throw new UnauthorizedAccessException("Access denied: Je kunt alleen je eigen voertuigen verwijderen");
        }

        // Gebruik ArchiveService om het voertuig en gerelateerde reserveringen te archiveren
        var reservationCount = await _archiveService.ArchiveVehicleAndReservationsAsync(vehicle, username);

        return $"Voertuig succesvol verwijderd en gearchiveerd (inclusief {reservationCount} reserveringen)";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check of kenteken al bestaat voor deze gebruiker
    /// </summary>
    private async Task<bool> LicensePlateExistsForUser(string licensePlate, int userId, int? excludeVehicleId = null)
    {
        var normalizedPlate = licensePlate.Replace("-", "").Replace(" ", "").ToUpper();
        
        var query = _context.Vehicles
            .Where(v => v.UserId == userId)
            .Where(v => v.LicensePlate.Replace("-", "").Replace(" ", "").ToUpper() == normalizedPlate);
        
        // Bij update: exclude het voertuig dat we aan het updaten zijn
        if (excludeVehicleId.HasValue)
            query = query.Where(v => v.Id != excludeVehicleId.Value);
        
        return await query.AnyAsync();
    }

    /// <summary>
    /// Valideer Nederlands kenteken format
    /// Ondersteunt alle Nederlandse kenteken formaten (6, 7 en 8 karakters)
    /// </summary>
    private bool IsValidDutchLicensePlate(string licensePlate)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            return false;

        // Verwijder streepjes en spaties, maak hoofdletters
        var cleaned = licensePlate.Replace("-", "").Replace(" ", "").ToUpper();
    
        // Check lengte (6-8 karakters voor Nederlandse kentekens)
        if (cleaned.Length < 6 || cleaned.Length > 8)
            return false;
    
        // Nederlandse kenteken formaten
        var patterns = new[]
        {
            // 6 karakters
            @"^[A-Z]{2}\d{2}\d{2}$",     // XX-99-99
            @"^\d{2}[A-Z]{2}\d{2}$",     // 99-XX-99
            @"^\d{2}\d{2}[A-Z]{2}$",     // 99-99-XX
            @"^[A-Z]{2}\d{2}[A-Z]{2}$",  // XX-99-XX
            @"^[A-Z]{2}[A-Z]{2}\d{2}$",  // XX-XX-99
            @"^\d{2}[A-Z]{2}[A-Z]{2}$",  // 99-XX-XX
        
            // 7 karakters
            @"^[A-Z]{2}\d{3}[A-Z]$",     // XX-999-X (bijv. AB-123-C)
            @"^[A-Z]\d{3}[A-Z]{2}$",     // X-999-XX
            @"^\d[A-Z]{2}\d{3}$",        // 9-XX-999
            @"^\d{3}[A-Z]{2}\d$",        // 999-XX-9
            @"^[A-Z]{3}\d{2}[A-Z]$",     // XXX-99-X
            @"^[A-Z]\d{2}[A-Z]{3}$",     // X-99-XXX
            @"^\d[A-Z]{3}\d{2}$",        // 9-XXX-99
            @"^\d{3}[A-Z]{3}$",          // 999-XXX
        
            // 8 karakters (oudere formaten)
            @"^[A-Z]{2}\d{4}$",          // XX-9999
            @"^\d{4}[A-Z]{2}$",          // 9999-XX
            @"^[A-Z]{3}\d{3}$",          // XXX-999
            @"^\d{3}[A-Z]{3}$"           // 999-XXX
        };
    
        return patterns.Any(p => Regex.IsMatch(cleaned, p));
    }

    private bool IsAdmin(string userRole)
    {
        return string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}