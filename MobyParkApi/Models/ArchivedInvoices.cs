using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_invoices")]
public class ArchivedInvoices
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
    
    [Column("invoice_date")]
    public DateTime? InvoiceDate { get; set; }
    
    [Column("due_date")]
    public DateTime? DueDate { get; set; }
    
    [Column("status")]
    public string? Status { get; set; }
    
    [Column("total_amount")]
    public decimal? TotalAmount { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at")]
    public DateTime? ModifiedAt { get; set; }
    
    [Required]
    [Column("archived_at")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

