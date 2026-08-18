using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("payments")]
public class Payments
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("parking_lot_id")]
    public int ParkingLotId { get; set; }

    [Column("parking_session_id")]
    public int? ParkingSessionId { get; set; } 

    [Column("invoice_id")]
    public int? InvoiceId { get; set; }

    [Column("license_plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [Column("duration")]
    public int Duration { get; set; }

    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "Pending";

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("cost")]
    public decimal Cost { get; set; }

    [Column("discount")]
    public decimal Discount { get; set; } = 0.0m;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [Column("discount_code_id")]
    public int? DiscountCodeId { get; set; }

    [ForeignKey("DiscountCodeId")]
    public virtual DiscountCodes? DiscountCode { get; set; }

}


