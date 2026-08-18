using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("discount_codes")]
public class DiscountCodes
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("discount_type")]
    [MaxLength(20)]
    public string DiscountType { get; set; } = string.Empty; // "Percentage" or "FixedAmount"

    [Required]
    [Column("discount_value")]
    public decimal DiscountValue { get; set; }

    [Column("start_date", TypeName = "timestamp without time zone")]
    public DateTime StartDate { get; set; }

    [Column("end_date", TypeName = "timestamp without time zone")]
    public DateTime? EndDate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("max_uses")]
    public int? MaxUses { get; set; }

    [Column("current_uses")]
    public int CurrentUses { get; set; } = 0;

    [Column("allowed_parking_lot_ids")]
    public string? AllowedParkingLotIds { get; set; } // JSON array of parking lot IDs, null = all

    [Column("allowed_time_ranges")]
    public string? AllowedTimeRanges { get; set; } // JSON array of time ranges, null = all times

    [Column("allowed_user_ids")]
    public string? AllowedUserIds { get; set; } // JSON array of user IDs, null = all users

    [Column("allowed_user_groups")]
    public string? AllowedUserGroups { get; set; } // JSON array of user groups/roles, null = all groups

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    // Navigation properties
    [ForeignKey("CreatedBy")]
    public virtual Users? Creator { get; set; }
}

