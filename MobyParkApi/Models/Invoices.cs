using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("invoices")]
public class Invoices
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Required]
    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = string.Empty;
    
    [Column("invoice_date", TypeName = "timestamp without time zone")]
    public DateTime? InvoiceDate { get; set; }
    
    [Column("due_date", TypeName = "timestamp without time zone")]
    public DateTime? DueDate { get; set; }
    
    [Column("status")]
    public string? Status { get; set; }
    
    [Column("total_amount")]
    public decimal? TotalAmount { get; set; }
    
    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }
}

