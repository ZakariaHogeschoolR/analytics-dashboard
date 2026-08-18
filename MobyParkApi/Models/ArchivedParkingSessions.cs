using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_parking_sessions")]
public class ArchivedParkingSessions
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("parking_lot_id")]
    public int ParkingLotId { get; set; }
    
    [Required]
    [Column("license_plate")]
    [MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;
    
    [Required]
    [Column("started")]
    public DateTime Started { get; set; }
    
    [Column("stopped")]
    public DateTime? Stopped { get; set; }
    
    [Column("user_id")]
    public int? UserId { get; set; }
    
    [Column("is_walk_up")]
    public bool IsWalkUp { get; set; }
    
    [Column("duration_minutes")]
    public int? DurationMinutes { get; set; }
    
    [Column("cost")]
    public decimal? Cost { get; set; }
    
    [Column("payment_status")]
    [MaxLength(20)]
    public string? PaymentStatus { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at")]
    public DateTime? ModifiedAt { get; set; }
    
    [Column("original_session_id")]
    public int? OriginalSessionId { get; set; }
    
    [Required]
    [Column("archived_at")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

