namespace MobyParkApi.Services;

using Microsoft.EntityFrameworkCore;
using MobyParkApi.DTOs;
using MobyParkApi.Models;
using MobyParkApi.Data;

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InvoiceService> _logger;
    private readonly IInvoiceArchiveService _archiveService;

    public InvoiceService(
        ApplicationDbContext context, 
        ILogger<InvoiceService> logger,
        IInvoiceArchiveService archiveService)
    {
        _context = context;
        _logger = logger;
        _archiveService = archiveService;
    }

    public async Task<List<InvoiceListItemDto>> GetInvoicesByUserIdAsync(int userId)
    {
        _logger.LogInformation("Fetching invoices for user {UserId}", userId);

        var invoices = await _context.Invoices
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new InvoiceListItemDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                Status = i.Status,
                TotalAmount = i.TotalAmount
            })
            .ToListAsync();

        _logger.LogInformation("Found {Count} invoices for user {UserId}", invoices.Count, userId);
        return invoices;
    }

    public async Task<InvoiceDetailDto?> GetInvoiceByIdAsync(int invoiceId, int userId)
    {
        _logger.LogInformation("Fetching invoice {InvoiceId} for user {UserId}", invoiceId, userId);

        var invoice = await _context.Invoices
            .Where(i => i.Id == invoiceId && i.UserId == userId)
            .Select(i => new InvoiceDetailDto
            {
                Id = i.Id,
                UserId = i.UserId,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                Status = i.Status,
                TotalAmount = i.TotalAmount,
                Payments = _context.Payments
                    .Where(p => p.InvoiceId == i.Id)
                    .Select(p => new PaymentItemDto
                    {
                        Id = p.Id,
                        LicensePlate = p.LicensePlate,
                        StartTime = p.StartTime,
                        EndTime = p.EndTime,
                        Duration = p.Duration,
                        Cost = p.Cost,
                        Discount = p.Discount,
                        PaymentStatus = p.PaymentStatus
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for user {UserId}", invoiceId, userId);
        }

        return invoice;
    }

    public async Task<PaymentResponseDto> PayInvoiceAsync(int invoiceId, int userId, PayInvoiceDto paymentDto)
    {
        _logger.LogInformation("Processing payment for invoice {InvoiceId} by user {UserId}", invoiceId, userId);

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId);

        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for user {UserId}", invoiceId, userId);
            throw new KeyNotFoundException($"Invoice {invoiceId} not found or does not belong to user {userId}");
        }

        if (invoice.Status == "paid")
        {
            _logger.LogWarning("Invoice {InvoiceId} is already paid", invoiceId);
            throw new InvalidOperationException($"Invoice {invoiceId} is already paid");
        }

        // Update invoice status
        invoice.Status = "paid";
        invoice.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Update alle gekoppelde payments naar paid
        var payments = await _context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .ToListAsync();

        foreach (var payment in payments)
        {
            payment.PaymentStatus = "paid";
            payment.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully processed payment for invoice {InvoiceId}", invoiceId);

        // Haal username op voor archived_by veld
        var user = await _context.Users.FindAsync(userId);
        var archivedBy = user?.Username ?? $"User_{userId}";

        // Optioneel: Automatisch archiveren na betaling
        // Dit kan worden uitgeschakeld als je liever manual archiving doet
        try
        {
            _logger.LogInformation("Auto-archiving paid invoice {InvoiceId}", invoiceId);
            var wasArchived = await _archiveService.ArchiveInvoiceAsync(invoiceId, archivedBy);
            
            if (wasArchived)
            {
                _logger.LogInformation("Invoice {InvoiceId} successfully auto-archived", invoiceId);
            }
            else
            {
                _logger.LogWarning("Invoice {InvoiceId} could not be auto-archived", invoiceId);
            }
        }
        catch (Exception ex)
        {
            // Archiving failure should not fail the payment
            _logger.LogError(ex, "Error auto-archiving invoice {InvoiceId}, but payment was successful", invoiceId);
        }

        return new PaymentResponseDto
        {
            Success = true,
            Message = "Payment processed successfully and invoice archived",
            InvoiceId = invoice.Id,
            AmountPaid = invoice.TotalAmount ?? 0,
            PaidAt = DateTime.UtcNow
        };
    }

    public async Task<bool> IsInvoiceOwnedByUserAsync(int invoiceId, int userId)
    {
        return await _context.Invoices
            .AnyAsync(i => i.Id == invoiceId && i.UserId == userId);
    }

    public async Task<GenerateInvoicesResultDto> GenerateMonthlyInvoicesAsync(int year, int month)
    {
        _logger.LogInformation("Generating monthly invoices for {Year}-{Month}", year, month);

        // Valideer input
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            throw new ArgumentException("Invalid year or month");
        }

        // Bereken start en eind datum van de maand (UTC, maar converteren naar Unspecified voor database)
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = DateTime.SpecifyKind(startDate.AddMonths(1).AddDays(-1), DateTimeKind.Unspecified);
        var dueDate = DateTime.SpecifyKind(endDate.AddDays(14), DateTimeKind.Unspecified);

        _logger.LogInformation("Processing payments from {StartDate} to {EndDate}", startDate, endDate);

        // Haal alle payments op zonder invoice_id voor deze maand
        var unbilledPayments = await _context.Payments
            .Where(p => p.InvoiceId == null &&
                       p.StartTime >= startDate &&
                       p.StartTime <= endDate.AddDays(1)) // Include hele laatste dag
            .ToListAsync();

        if (!unbilledPayments.Any())
        {
            _logger.LogInformation("No unbilled payments found for {Year}-{Month}", year, month);
            return new GenerateInvoicesResultDto
            {
                Success = true,
                InvoicesGenerated = 0,
                UsersProcessed = 0,
                TotalAmount = 0,
                Message = $"No unbilled payments found for {year}-{month:D2}"
            };
        }

        // Groepeer payments per user_id
        var paymentsByUser = unbilledPayments
            .Where(p => p.UserId.HasValue)
            .GroupBy(p => p.UserId!.Value)
            .ToList();

        var result = new GenerateInvoicesResultDto
        {
            Success = true,
            UsersProcessed = paymentsByUser.Count
        };

        // Genereer invoice number sequence
        var existingInvoicesCount = await _context.Invoices
            .CountAsync(i => i.InvoiceDate != null && 
                           i.InvoiceDate.Value.Year == year && 
                           i.InvoiceDate.Value.Month == month);

        int sequenceNumber = existingInvoicesCount + 1;

        // Voor elke gebruiker, maak een factuur aan
        foreach (var userPayments in paymentsByUser)
        {
            var userId = userPayments.Key;
            var payments = userPayments.ToList();

            // Bereken totaal bedrag (cost - discount)
            var totalAmount = payments.Sum(p => p.Cost - p.Discount);

            // Genereer invoice number
            var invoiceNumber = $"INV-{year}-{month:D2}-{sequenceNumber:D3}";

            // Maak nieuwe invoice aan
            var invoice = new Invoices
            {
                UserId = userId,
                InvoiceNumber = invoiceNumber,
                InvoiceDate = endDate,
                DueDate = dueDate,
                Status = "open",
                TotalAmount = totalAmount,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(); // Save om ID te krijgen

            _logger.LogInformation("Created invoice {InvoiceNumber} for user {UserId} with total {TotalAmount}", 
                invoiceNumber, userId, totalAmount);

            // Update alle payments met de nieuwe invoice_id
            foreach (var payment in payments)
            {
                payment.InvoiceId = invoice.Id;
                payment.ModifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            }

            // Voeg toe aan result
            result.GeneratedInvoices.Add(new GeneratedInvoiceDto
            {
                InvoiceId = invoice.Id,
                UserId = userId,
                InvoiceNumber = invoiceNumber,
                TotalAmount = totalAmount,
                PaymentCount = payments.Count
            });

            result.InvoicesGenerated++;
            result.TotalAmount += totalAmount;
            sequenceNumber++;
        }

        // Save alle payment updates
        await _context.SaveChangesAsync();

        result.Message = $"Successfully generated {result.InvoicesGenerated} invoices for {result.UsersProcessed} users, total amount: €{result.TotalAmount:F2}";
        
        _logger.LogInformation("Invoice generation completed: {Message}", result.Message);

        return result;
    }
}