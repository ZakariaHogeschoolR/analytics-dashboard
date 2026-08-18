namespace MobyParkApi.DTOs;

// DTO voor lijst van facturen (GET /api/invoices)
public class InvoiceListItemDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public decimal? TotalAmount { get; set; }
}

// DTO voor factuur details (GET /api/invoices/{id})
public class InvoiceDetailDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public decimal? TotalAmount { get; set; }
    public List<PaymentItemDto> Payments { get; set; } = new();
}

// DTO voor payment items in een factuur
public class PaymentItemDto
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Duration { get; set; }
    public decimal Cost { get; set; }
    public decimal Discount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

// DTO voor betaling verwerken (POST /api/invoices/{id}/pay)
public class PayInvoiceDto
{
    public string PaymentMethod { get; set; } = string.Empty; // bijv. "creditcard", "ideal", "paypal"
}

// Response DTO na succesvolle betaling
public class PaymentResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime PaidAt { get; set; }
}

// DTO voor het resultaat van factuur generatie
public class GenerateInvoicesResultDto
{
    public bool Success { get; set; }
    public int InvoicesGenerated { get; set; }
    public int UsersProcessed { get; set; }
    public decimal TotalAmount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<GeneratedInvoiceDto> GeneratedInvoices { get; set; } = new();
}

// DTO voor een gegenereerde factuur
public class GeneratedInvoiceDto
{
    public int InvoiceId { get; set; }
    public int UserId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
}