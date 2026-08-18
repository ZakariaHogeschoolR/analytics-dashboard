using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_reservations")]
public class ArchivedReservations
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Required]
    [Column("parking_lot_id")]
    public int ParkingLotId { get; set; }
    
    [Required]
    [Column("vehicle_id")]
    public int VehicleId { get; set; }
    
    [Column("start_time", TypeName = "timestamp without time zone")]
    public DateTime StartTime { get; set; }
    
    [Column("end_date")]
    public DateOnly? EndDate { get; set; }
    
    [Column("end_time", TypeName = "time")]
    public TimeOnly? EndTime { get; set; }
    
    // Computed property voor gemakkelijke toegang (niet gemapped naar database)
    [NotMapped]
    public DateTime? EndDateTime
    {
        get
        {
            if (EndDate.HasValue && EndTime.HasValue)
            {
                return EndDate.Value.ToDateTime(EndTime.Value);
            }
            return null;
        }
        set
        {
            if (value.HasValue)
            {
                EndDate = DateOnly.FromDateTime(value.Value);
                EndTime = TimeOnly.FromDateTime(value.Value);
            }
            else
            {
                EndDate = null;
                EndTime = null;
            }
        }
    }

    [Required]
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    
    [Column("cost")]
    public decimal Cost { get; set; }
    
    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }
    
    [Required]
    [Column("archived_at", TypeName = "timestamp without time zone")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

