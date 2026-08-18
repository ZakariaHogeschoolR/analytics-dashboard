using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("discount_code_usage")]
public class DiscountCodeUsage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("discount_code_id")]
    public int DiscountCodeId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("reservation_id")]
    public int? ReservationId { get; set; }

    [Column("payment_id")]
    public int? PaymentId { get; set; }

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [Column("original_cost")]
    public decimal OriginalCost { get; set; }

    [Column("final_cost")]
    public decimal FinalCost { get; set; }

    [Column("used_at", TypeName = "timestamp without time zone")]
    public DateTime UsedAt { get; set; }

    // Navigation properties
    [ForeignKey("DiscountCodeId")]
    public virtual DiscountCodes? DiscountCode { get; set; }

    [ForeignKey("UserId")]
    public virtual Users? User { get; set; }

    [ForeignKey("ReservationId")]
    public virtual Reservations? Reservation { get; set; }

    [ForeignKey("PaymentId")]
    public virtual Payments? Payment { get; set; }
}

