using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

public class CreateDiscountCodeDto
{
    [Required(ErrorMessage = "Code is verplicht")]
    [MinLength(3, ErrorMessage = "Code moet minimaal 3 tekens bevatten")]
    [MaxLength(50, ErrorMessage = "Code mag maximaal 50 tekens bevatten")]
    [RegularExpression(@"^[A-Z0-9\-_]+$", ErrorMessage = "Code mag alleen hoofdletters, cijfers, streepjes en underscores bevatten")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kortingstype is verplicht")]
    [RegularExpression(@"^(Percentage|FixedAmount)$", ErrorMessage = "Kortingstype moet 'Percentage' of 'FixedAmount' zijn")]
    public string DiscountType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kortingswaarde is verplicht")]
    [Range(0.01, 999999.99, ErrorMessage = "Kortingswaarde moet tussen 0.01 en 999999.99 liggen")]
    public decimal DiscountValue { get; set; }

    [Required(ErrorMessage = "Startdatum is verplicht")]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "Maximaal aantal gebruiken moet groter zijn dan 0")]
    public int? MaxUses { get; set; }

    public List<int>? AllowedParkingLotIds { get; set; }

    public List<TimeRangeDto>? AllowedTimeRanges { get; set; }

    public List<int>? AllowedUserIds { get; set; }

    public List<string>? AllowedUserGroups { get; set; }
}

public class TimeRangeDto
{
    [Required]
    [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Tijd moet in formaat HH:MM zijn")]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Tijd moet in formaat HH:MM zijn")]
    public string EndTime { get; set; } = string.Empty;

    public List<int>? DaysOfWeek { get; set; } // 0 = Sunday, 1 = Monday, etc.
}

public class UpdateDiscountCodeDto
{
    public string? DiscountType { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "Kortingswaarde moet tussen 0.01 en 999999.99 liggen")]
    public decimal? DiscountValue { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Maximaal aantal gebruiken moet groter zijn dan 0")]
    public int? MaxUses { get; set; }

    public List<int>? AllowedParkingLotIds { get; set; }

    public List<TimeRangeDto>? AllowedTimeRanges { get; set; }

    public List<int>? AllowedUserIds { get; set; }

    public List<string>? AllowedUserGroups { get; set; }
}

public class DiscountCodeResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public List<int>? AllowedParkingLotIds { get; set; }
    public List<TimeRangeDto>? AllowedTimeRanges { get; set; }
    public List<int>? AllowedUserIds { get; set; }
    public List<string>? AllowedUserGroups { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string Status { get; set; } = string.Empty; // "Active", "Expired", "Inactive", "MaxUsesReached"
    public DiscountCodeStatisticsDto? Statistics { get; set; }
}

public class DiscountCodeStatisticsDto
{
    public int TotalUses { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal TotalOriginalAmount { get; set; }
    public decimal ConversionRate { get; set; }
    public List<DiscountCodeUsageDto>? RecentUsage { get; set; }
}

public class DiscountCodeUsageDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? ReservationId { get; set; }
    public int? PaymentId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal OriginalCost { get; set; }
    public decimal FinalCost { get; set; }
    public DateTime UsedAt { get; set; }
}

public class ValidateDiscountCodeDto
{
    [Required(ErrorMessage = "Kortingscode is verplicht")]
    public string Code { get; set; } = string.Empty;

    public int? ParkingLotId { get; set; }

    public DateTime? ReservationStartTime { get; set; }

    public decimal? OriginalCost { get; set; }
}

public class DiscountCodeValidationResultDto
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalCost { get; set; }
    public DiscountCodeResponseDto? DiscountCode { get; set; }
}

