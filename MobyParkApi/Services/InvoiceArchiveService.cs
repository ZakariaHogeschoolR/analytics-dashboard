namespace MobyParkApi.Services;

using Microsoft.EntityFrameworkCore;
using MobyParkApi.Models;
using MobyParkApi.Data;

public class InvoiceArchiveService : IInvoiceArchiveService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InvoiceArchiveService> _logger;

    public InvoiceArchiveService(
        ApplicationDbContext context,
        ILogger<InvoiceArchiveService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ArchiveInvoiceAsync(int invoiceId, string archivedBy)
    {
        _logger.LogInformation("Attempting to archive invoice {InvoiceId} by {ArchivedBy}", invoiceId, archivedBy);

        // Haal invoice op
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found", invoiceId);
            return false;
        }

        // Check of invoice betaald is
        if (invoice.Status != "paid")
        {
            _logger.LogWarning("Cannot archive invoice {InvoiceId} - status is {Status}, must be 'paid'", 
                invoiceId, invoice.Status);
            return false;
        }

        // Check of alle gekoppelde payments ook paid zijn
        var payments = await _context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .ToListAsync();

        var unpaidPayments = payments.Where(p => p.PaymentStatus != "paid").ToList();
        if (unpaidPayments.Any())
        {
            _logger.LogWarning("Cannot archive invoice {InvoiceId} - {Count} payments are not paid", 
                invoiceId, unpaidPayments.Count);
            return false;
        }

        // Maak archive record aan - converteer alle DateTime velden naar UTC voor PostgreSQL
        var archivedInvoice = new ArchivedInvoices
        {
            UserId = invoice.UserId,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate.HasValue 
                ? (invoice.InvoiceDate.Value.Kind == DateTimeKind.Utc 
                    ? invoice.InvoiceDate.Value 
                    : DateTime.SpecifyKind(invoice.InvoiceDate.Value, DateTimeKind.Utc))
                : (DateTime?)null,
            DueDate = invoice.DueDate.HasValue 
                ? (invoice.DueDate.Value.Kind == DateTimeKind.Utc 
                    ? invoice.DueDate.Value 
                    : DateTime.SpecifyKind(invoice.DueDate.Value, DateTimeKind.Utc))
                : (DateTime?)null,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt.Kind == DateTimeKind.Utc 
                ? invoice.CreatedAt 
                : DateTime.SpecifyKind(invoice.CreatedAt, DateTimeKind.Utc),
            ModifiedAt = invoice.ModifiedAt.HasValue 
                ? (invoice.ModifiedAt.Value.Kind == DateTimeKind.Utc 
                    ? invoice.ModifiedAt.Value 
                    : DateTime.SpecifyKind(invoice.ModifiedAt.Value, DateTimeKind.Utc))
                : (DateTime?)null,
            ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            ArchivedBy = archivedBy
        };

        _context.ArchivedInvoices.Add(archivedInvoice);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Invoice {InvoiceId} copied to archive as {ArchiveId} by {ArchivedBy}", 
            invoiceId, archivedInvoice.Id, archivedBy);

        // Verwijder invoice uit main tabel
        // Note: Payments behouden hun invoice_id als historische referentie
        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Invoice {InvoiceId} successfully archived and removed from active invoices", 
            invoiceId);

        return true;
    }

    public async Task<int> ArchiveAllPaidInvoicesAsync(string archivedBy)
    {
        _logger.LogInformation("Starting batch archiving of all paid invoices by {ArchivedBy}", archivedBy);

        // Haal alle paid invoices op
        var paidInvoices = await _context.Invoices
            .Where(i => i.Status == "paid")
            .ToListAsync();

        _logger.LogInformation("Found {Count} paid invoices to archive", paidInvoices.Count);

        int archivedCount = 0;

        foreach (var invoice in paidInvoices)
        {
            try
            {
                var wasArchived = await ArchiveInvoiceAsync(invoice.Id, archivedBy);
                if (wasArchived)
                {
                    archivedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving invoice {InvoiceId}", invoice.Id);
                // Continue met volgende invoice
            }
        }

        _logger.LogInformation("Successfully archived {Count} invoices", archivedCount);
        return archivedCount;
    }

    public async Task<ArchivedInvoices?> GetArchivedInvoiceAsync(int archiveId)
    {
        return await _context.ArchivedInvoices
            .FirstOrDefaultAsync(i => i.Id == archiveId);
    }

    public async Task<List<ArchivedInvoices>> GetArchivedInvoicesForUserAsync(int userId)
    {
        return await _context.ArchivedInvoices
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.ArchivedAt)
            .ToListAsync();
    }
}