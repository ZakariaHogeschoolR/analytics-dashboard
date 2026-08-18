using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_payments")]
public class ArchivedPayments
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("parking_lot_id")]
    public int ParkingLotId { get; set; }
    
    [Column("parking_session_id")]
    public int? ParkingSessionId { get; set; }
    
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Column("invoice_id")]
    public int? InvoiceId { get; set; }
    
    [Column("license_plate")]
    public string LicensePlate { get; set; } = string.Empty;
    
    [Column("duration")]
    public int Duration { get; set; }
    
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = string.Empty;
    
    [Column("start_time")]
    public DateTime StartTime { get; set; }
    
    [Column("end_time")]
    public DateTime EndTime { get; set; }
    
    [Column("cost")]
    public double Cost { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at")]
    public DateTime ModifiedAt { get; set; }
    
    [Column("discount")]
    public double Discount { get; set; }
    
    [Required]
    [Column("archived_at")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public int ArchivedBy { get; set; }
}

