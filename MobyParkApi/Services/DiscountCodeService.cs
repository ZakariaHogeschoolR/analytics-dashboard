using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MobyParkApi.Services;

public interface IDiscountCodeService
{
    Task<DiscountCodeResponseDto> CreateDiscountCodeAsync(CreateDiscountCodeDto dto, int createdBy);
    Task<DiscountCodeResponseDto?> GetDiscountCodeByIdAsync(int id);
    Task<DiscountCodeResponseDto?> GetDiscountCodeByCodeAsync(string code);
    Task<List<DiscountCodeResponseDto>> GetAllDiscountCodesAsync(bool? activeOnly = null);
    Task<DiscountCodeResponseDto> UpdateDiscountCodeAsync(int id, UpdateDiscountCodeDto dto);
    Task<bool> DeactivateDiscountCodeAsync(int id);
    Task<DiscountCodeValidationResultDto> ValidateDiscountCodeAsync(
        string code,
        int? userId,
        int? parkingLotId,
        DateTime? reservationStartTime,
        decimal originalCost);
    Task<decimal> ApplyDiscountCodeAsync(
        string code,
        int? userId,
        int? parkingLotId,
        DateTime? reservationStartTime,
        decimal originalCost,
        int? reservationId,
        int? paymentId);
    Task<DiscountCodeStatisticsDto> GetDiscountCodeStatisticsAsync(int discountCodeId);
}

public class DiscountCodeService : IDiscountCodeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiscountCodeService> _logger;

    public DiscountCodeService(ApplicationDbContext context, ILogger<DiscountCodeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DiscountCodeResponseDto> CreateDiscountCodeAsync(CreateDiscountCodeDto dto, int createdBy)
    {
        _logger.LogInformation("Aanmaken kortingscode {Code} door user {UserId}", dto.Code, createdBy);

        // Check if code already exists
        var existingCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == dto.Code.ToUpper());

        if (existingCode != null)
        {
            _logger.LogWarning("Kortingscode {Code} bestaat al", dto.Code);
            throw new ArgumentException("Kortingscode bestaat al");
        }

        // Validate discount value based on type
        if (dto.DiscountType == "Percentage" && (dto.DiscountValue < 0 || dto.DiscountValue > 100))
        {
            throw new ArgumentException("Percentage korting moet tussen 0 en 100 liggen");
        }

        if (dto.DiscountType == "FixedAmount" && dto.DiscountValue <= 0)
        {
            throw new ArgumentException("Vast bedrag korting moet groter zijn dan 0");
        }

        // Validate dates
        if (dto.EndDate.HasValue && dto.EndDate.Value <= dto.StartDate)
        {
            throw new ArgumentException("Einddatum moet na startdatum zijn");
        }

        var discountCode = new DiscountCodes
        {
            Code = dto.Code.ToUpper(),
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            MaxUses = dto.MaxUses,
            CurrentUses = 0,
            AllowedParkingLotIds = dto.AllowedParkingLotIds != null && dto.AllowedParkingLotIds.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedParkingLotIds) 
                : null,
            AllowedTimeRanges = dto.AllowedTimeRanges != null && dto.AllowedTimeRanges.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedTimeRanges) 
                : null,
            AllowedUserIds = dto.AllowedUserIds != null && dto.AllowedUserIds.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedUserIds) 
                : null,
            AllowedUserGroups = dto.AllowedUserGroups != null && dto.AllowedUserGroups.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedUserGroups) 
                : null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _context.DiscountCodes.Add(discountCode);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Kortingscode {Code} (ID: {Id}) aangemaakt", discountCode.Code, discountCode.Id);

            return await MapToResponseDtoAsync(discountCode, includeStatistics: true);
    }

    public async Task<DiscountCodeResponseDto?> GetDiscountCodeByIdAsync(int id)
    {
        var discountCode = await _context.DiscountCodes.FindAsync(id);
        return discountCode == null ? null : await MapToResponseDtoAsync(discountCode);
    }

    public async Task<DiscountCodeResponseDto?> GetDiscountCodeByCodeAsync(string code)
    {
        var discountCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == code.ToUpper());
        return discountCode == null ? null : await MapToResponseDtoAsync(discountCode);
    }

    public async Task<List<DiscountCodeResponseDto>> GetAllDiscountCodesAsync(bool? activeOnly = null)
    {
        var query = _context.DiscountCodes.AsQueryable();

        if (activeOnly == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(dc => dc.IsActive &&
                dc.StartDate <= now &&
                (dc.EndDate == null || dc.EndDate >= now) &&
                (dc.MaxUses == null || dc.CurrentUses < dc.MaxUses));
        }

        var discountCodes = await query.OrderByDescending(dc => dc.CreatedAt).ToListAsync();
        var result = new List<DiscountCodeResponseDto>();

        foreach (var code in discountCodes)
        {
            result.Add(await MapToResponseDtoAsync(code, includeStatistics: false));
        }

        return result;
    }

    public async Task<DiscountCodeResponseDto> UpdateDiscountCodeAsync(int id, UpdateDiscountCodeDto dto)
    {
        _logger.LogInformation("Updaten kortingscode {Id}", id);

        var discountCode = await _context.DiscountCodes.FindAsync(id);
        if (discountCode == null)
        {
            throw new KeyNotFoundException("Kortingscode niet gevonden");
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(dto.DiscountType))
        {
            discountCode.DiscountType = dto.DiscountType;
        }

        if (dto.DiscountValue.HasValue)
        {
            if (discountCode.DiscountType == "Percentage" && (dto.DiscountValue.Value < 0 || dto.DiscountValue.Value > 100))
            {
                throw new ArgumentException("Percentage korting moet tussen 0 en 100 liggen");
            }
            if (discountCode.DiscountType == "FixedAmount" && dto.DiscountValue.Value <= 0)
            {
                throw new ArgumentException("Vast bedrag korting moet groter zijn dan 0");
            }
            discountCode.DiscountValue = dto.DiscountValue.Value;
        }

        if (dto.StartDate.HasValue)
        {
            discountCode.StartDate = dto.StartDate.Value;
        }

        if (dto.EndDate.HasValue)
        {
            if (dto.EndDate.Value <= discountCode.StartDate)
            {
                throw new ArgumentException("Einddatum moet na startdatum zijn");
            }
            discountCode.EndDate = dto.EndDate;
        }

        if (dto.IsActive.HasValue)
        {
            discountCode.IsActive = dto.IsActive.Value;
        }

        if (dto.MaxUses.HasValue)
        {
            if (dto.MaxUses.Value < discountCode.CurrentUses)
            {
                throw new ArgumentException("Maximaal aantal gebruiken kan niet kleiner zijn dan huidig aantal gebruiken");
            }
            discountCode.MaxUses = dto.MaxUses;
        }

        if (dto.AllowedParkingLotIds != null)
        {
            // Als array leeg is, zet op null (geen restricties)
            discountCode.AllowedParkingLotIds = dto.AllowedParkingLotIds.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedParkingLotIds) 
                : null;
        }

        if (dto.AllowedTimeRanges != null)
        {
            discountCode.AllowedTimeRanges = dto.AllowedTimeRanges.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedTimeRanges) 
                : null;
        }

        if (dto.AllowedUserIds != null)
        {
            discountCode.AllowedUserIds = dto.AllowedUserIds.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedUserIds) 
                : null;
        }

        if (dto.AllowedUserGroups != null)
        {
            discountCode.AllowedUserGroups = dto.AllowedUserGroups.Count > 0 
                ? JsonSerializer.Serialize(dto.AllowedUserGroups) 
                : null;
        }

        discountCode.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Kortingscode {Id} geüpdatet", id);

            return await MapToResponseDtoAsync(discountCode, includeStatistics: true);
    }

    public async Task<bool> DeactivateDiscountCodeAsync(int id)
    {
        _logger.LogInformation("Deactiveren kortingscode {Id}", id);

        var discountCode = await _context.DiscountCodes.FindAsync(id);
        if (discountCode == null)
        {
            throw new KeyNotFoundException("Kortingscode niet gevonden");
        }

        discountCode.IsActive = false;
        discountCode.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Kortingscode {Id} gedeactiveerd", id);

        return true;
    }

    public async Task<DiscountCodeValidationResultDto> ValidateDiscountCodeAsync(
        string code,
        int? userId,
        int? parkingLotId,
        DateTime? reservationStartTime,
        decimal originalCost)
    {
        _logger.LogDebug("Valideren kortingscode {Code} voor user {UserId}, parkingLot {ParkingLotId}", 
            code, userId, parkingLotId);

        var discountCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == code.ToUpper());

        if (discountCode == null)
        {
            return new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode niet gevonden"
            };
        }

        // Check if active
        if (!discountCode.IsActive)
        {
            return new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode is niet actief"
            };
        }

        // Check dates
        var now = DateTime.UtcNow;
        if (discountCode.StartDate > now)
        {
            return new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode is nog niet geldig"
            };
        }

        if (discountCode.EndDate.HasValue && discountCode.EndDate.Value < now)
        {
            return new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode is verlopen"
            };
        }

        // Check max uses
        if (discountCode.MaxUses.HasValue && discountCode.CurrentUses >= discountCode.MaxUses.Value)
        {
            return new DiscountCodeValidationResultDto
            {
                IsValid = false,
                ErrorMessage = "Kortingscode heeft maximale aantal gebruiken bereikt"
            };
        }

        // Check user restrictions
        if (!string.IsNullOrWhiteSpace(discountCode.AllowedUserIds))
        {
            var allowedUserIds = JsonSerializer.Deserialize<List<int>>(discountCode.AllowedUserIds);
            if (allowedUserIds != null && allowedUserIds.Any() && userId.HasValue && !allowedUserIds.Contains(userId.Value))
            {
                return new DiscountCodeValidationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "Kortingscode is niet geldig voor deze gebruiker"
                };
            }
        }

        // Check user group restrictions
        if (!string.IsNullOrWhiteSpace(discountCode.AllowedUserGroups) && userId.HasValue)
        {
            var allowedGroups = JsonSerializer.Deserialize<List<string>>(discountCode.AllowedUserGroups);
            if (allowedGroups != null && allowedGroups.Any())
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null || !allowedGroups.Contains(user.Role, StringComparer.OrdinalIgnoreCase))
                {
                    return new DiscountCodeValidationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = "Kortingscode is niet geldig voor uw gebruikersgroep"
                    };
                }
            }
        }

        // Check parking lot restrictions
        if (!string.IsNullOrWhiteSpace(discountCode.AllowedParkingLotIds) && parkingLotId.HasValue)
        {
            var allowedParkingLotIds = JsonSerializer.Deserialize<List<int>>(discountCode.AllowedParkingLotIds);
            if (allowedParkingLotIds != null && allowedParkingLotIds.Any() && !allowedParkingLotIds.Contains(parkingLotId.Value))
            {
                return new DiscountCodeValidationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "Kortingscode is niet geldig voor deze parkeerplaats"
                };
            }
        }

        // Check time restrictions
        if (!string.IsNullOrWhiteSpace(discountCode.AllowedTimeRanges) && reservationStartTime.HasValue)
        {
            var allowedTimeRanges = JsonSerializer.Deserialize<List<TimeRangeDto>>(discountCode.AllowedTimeRanges);
            if (allowedTimeRanges != null && allowedTimeRanges.Any())
            {
                var reservationTime = reservationStartTime.Value;
                var dayOfWeek = (int)reservationTime.DayOfWeek;
                var timeOfDay = reservationTime.TimeOfDay;

                var isValidTime = allowedTimeRanges.Any(tr =>
                {
                    var startTime = TimeSpan.Parse(tr.StartTime);
                    var endTime = TimeSpan.Parse(tr.EndTime);
                    var isValidDay = tr.DaysOfWeek == null || tr.DaysOfWeek.Count == 0 || tr.DaysOfWeek.Contains(dayOfWeek);
                    var isValidTimeOfDay = timeOfDay >= startTime && timeOfDay <= endTime;
                    return isValidDay && isValidTimeOfDay;
                });

                if (!isValidTime)
                {
                    return new DiscountCodeValidationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = "Kortingscode is niet geldig voor deze tijd"
                    };
                }
            }
        }

        // Calculate discount amount
        decimal discountAmount = 0;
        if (discountCode.DiscountType == "Percentage")
        {
            discountAmount = Math.Round(originalCost * (discountCode.DiscountValue / 100), 2, MidpointRounding.AwayFromZero);
        }
        else if (discountCode.DiscountType == "FixedAmount")
        {
            discountAmount = Math.Min(discountCode.DiscountValue, originalCost);
        }

        var finalCost = Math.Max(0, originalCost - discountAmount);

        return new DiscountCodeValidationResultDto
        {
            IsValid = true,
            DiscountAmount = discountAmount,
            FinalCost = finalCost,
            DiscountCode = await MapToResponseDtoAsync(discountCode, includeStatistics: false)
        };
    }

    public async Task<decimal> ApplyDiscountCodeAsync(
        string code,
        int? userId,
        int? parkingLotId,
        DateTime? reservationStartTime,
        decimal originalCost,
        int? reservationId,
        int? paymentId)
    {
        _logger.LogInformation("Toepassen kortingscode {Code} voor user {UserId}", code, userId);

        // Validate first
        var validation = await ValidateDiscountCodeAsync(code, userId, parkingLotId, reservationStartTime, originalCost);
        
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage ?? "Kortingscode is niet geldig");
        }

        var discountCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == code.ToUpper());

        if (discountCode == null)
        {
            throw new KeyNotFoundException("Kortingscode niet gevonden");
        }

        // Increment usage count
        discountCode.CurrentUses++;
        discountCode.ModifiedAt = DateTime.UtcNow;

        // Log usage
        var usage = new DiscountCodeUsage
        {
            DiscountCodeId = discountCode.Id,
            UserId = userId,
            ReservationId = reservationId,
            PaymentId = paymentId,
            DiscountAmount = validation.DiscountAmount,
            OriginalCost = originalCost,
            FinalCost = validation.FinalCost,
            UsedAt = DateTime.UtcNow
        };

        _context.DiscountCodeUsage.Add(usage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Kortingscode {Code} toegepast: €{DiscountAmount} korting op €{OriginalCost} = €{FinalCost}",
            code, validation.DiscountAmount, originalCost, validation.FinalCost);

        return validation.DiscountAmount;
    }

    public async Task<DiscountCodeStatisticsDto> GetDiscountCodeStatisticsAsync(int discountCodeId)
    {
        var discountCode = await _context.DiscountCodes.FindAsync(discountCodeId);
        if (discountCode == null)
        {
            throw new KeyNotFoundException("Kortingscode niet gevonden");
        }

        var usages = await _context.DiscountCodeUsage
            .Where(u => u.DiscountCodeId == discountCodeId)
            .Include(u => u.User)
            .OrderByDescending(u => u.UsedAt)
            .Take(10)
            .ToListAsync();

        var totalUses = discountCode.CurrentUses;
        var totalDiscountAmount = usages.Sum(u => u.DiscountAmount);
        var totalOriginalAmount = usages.Sum(u => u.OriginalCost);
        
        // Calculate conversion rate (if we had total attempts, but we don't track that, so we'll use a placeholder)
        var conversionRate = 0.0m; // Would need to track validation attempts to calculate this

        var recentUsage = usages.Select(u => new DiscountCodeUsageDto
        {
            Id = u.Id,
            UserId = u.UserId,
            Username = u.User?.Username,
            ReservationId = u.ReservationId,
            PaymentId = u.PaymentId,
            DiscountAmount = u.DiscountAmount,
            OriginalCost = u.OriginalCost,
            FinalCost = u.FinalCost,
            UsedAt = u.UsedAt
        }).ToList();

        return new DiscountCodeStatisticsDto
        {
            TotalUses = totalUses,
            TotalDiscountAmount = totalDiscountAmount,
            TotalOriginalAmount = totalOriginalAmount,
            ConversionRate = conversionRate,
            RecentUsage = recentUsage
        };
    }

    private async Task<DiscountCodeResponseDto> MapToResponseDtoAsync(DiscountCodes discountCode, bool includeStatistics = false)
    {
        var now = DateTime.UtcNow;
        var status = "Active";

        if (!discountCode.IsActive)
        {
            status = "Inactive";
        }
        else if (discountCode.StartDate > now)
        {
            status = "NotStarted";
        }
        else if (discountCode.EndDate.HasValue && discountCode.EndDate.Value < now)
        {
            status = "Expired";
        }
        else if (discountCode.MaxUses.HasValue && discountCode.CurrentUses >= discountCode.MaxUses.Value)
        {
            status = "MaxUsesReached";
        }

        DiscountCodeStatisticsDto? statistics = null;
        if (includeStatistics)
        {
            statistics = await GetDiscountCodeStatisticsAsync(discountCode.Id);
        }

        return new DiscountCodeResponseDto
        {
            Id = discountCode.Id,
            Code = discountCode.Code,
            DiscountType = discountCode.DiscountType,
            DiscountValue = discountCode.DiscountValue,
            StartDate = discountCode.StartDate,
            EndDate = discountCode.EndDate,
            IsActive = discountCode.IsActive,
            MaxUses = discountCode.MaxUses,
            CurrentUses = discountCode.CurrentUses,
            AllowedParkingLotIds = !string.IsNullOrWhiteSpace(discountCode.AllowedParkingLotIds) 
                ? JsonSerializer.Deserialize<List<int>>(discountCode.AllowedParkingLotIds) 
                : null,
            AllowedTimeRanges = !string.IsNullOrWhiteSpace(discountCode.AllowedTimeRanges) 
                ? JsonSerializer.Deserialize<List<TimeRangeDto>>(discountCode.AllowedTimeRanges) 
                : null,
            AllowedUserIds = !string.IsNullOrWhiteSpace(discountCode.AllowedUserIds) 
                ? JsonSerializer.Deserialize<List<int>>(discountCode.AllowedUserIds) 
                : null,
            AllowedUserGroups = !string.IsNullOrWhiteSpace(discountCode.AllowedUserGroups) 
                ? JsonSerializer.Deserialize<List<string>>(discountCode.AllowedUserGroups) 
                : null,
            CreatedAt = discountCode.CreatedAt,
            ModifiedAt = discountCode.ModifiedAt,
            Status = status,
            Statistics = statistics
        };
    }
}

