namespace MobyParkApi.Services;

using MobyParkApi.DTOs;

public interface IInvoiceService
{
    /// <summary>
    /// Haalt alle facturen op voor een specifieke gebruiker
    /// </summary>
    Task<List<InvoiceListItemDto>> GetInvoicesByUserIdAsync(int userId);

    /// <summary>
    /// Haalt details op van een specifieke factuur inclusief gekoppelde betalingen
    /// </summary>
    Task<InvoiceDetailDto?> GetInvoiceByIdAsync(int invoiceId, int userId);

    /// <summary>
    /// Verwerkt de betaling van een factuur
    /// </summary>
    Task<PaymentResponseDto> PayInvoiceAsync(int invoiceId, int userId, PayInvoiceDto paymentDto);

    /// <summary>
    /// Controleert of een factuur bij een specifieke gebruiker hoort
    /// </summary>
    Task<bool> IsInvoiceOwnedByUserAsync(int invoiceId, int userId);

    /// <summary>
    /// Genereert maandelijkse facturen voor alle gebruikers met onbetaalde payments
    /// </summary>
    Task<GenerateInvoicesResultDto> GenerateMonthlyInvoicesAsync(int year, int month);
}