using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace MobyParkApi.Models;

[Table("reservations")]
public class Reservations
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("parking_lot_id")]
    public int ParkingLotId { get; set; }

    [Column("vehicle_id")]
    public int VehicleId { get; set; }

    [Column("start_time", TypeName = "timestamp without time zone")]
    public DateTime StartTime { get; set; }

    [Column("end_time", TypeName = "timestamp without time zone")]
    public DateTime? EndTime { get; set; }  // ← Nullable! Voor oude records

    [Required]
    [Column("status")]
    public string Status { get; set; } = String.Empty;

    [Column("cost")]
    public decimal Cost { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual Users? User { get; set; }
    
    [ForeignKey("ParkingLotId")]
    public virtual ParkingLots? ParkingLot { get; set; }
    
    [ForeignKey("VehicleId")]
    public virtual Vehicles? Vehicle { get; set; }

    [Column("discount_code_id")]
    public int? DiscountCodeId { get; set; }

    [ForeignKey("DiscountCodeId")]
    public virtual DiscountCodes? DiscountCode { get; set; }
}