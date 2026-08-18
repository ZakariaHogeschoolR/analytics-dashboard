namespace MobyParkApi.Services;

public interface IInvoiceArchiveService
{
    /// <summary>
    /// Archiveert een specifieke betaalde invoice
    /// </summary>
    /// <param name="invoiceId">ID van de invoice om te archiveren</param>
    /// <param name="archivedBy">Naam van gebruiker die archiveert</param>
    /// <returns>True als succesvol gearchiveerd, false als niet mogelijk</returns>
    Task<bool> ArchiveInvoiceAsync(int invoiceId, string archivedBy);

    /// <summary>
    /// Archiveert alle betaalde invoices
    /// </summary>
    /// <param name="archivedBy">Naam van gebruiker die archiveert</param>
    /// <returns>Aantal gearchiveerde invoices</returns>
    Task<int> ArchiveAllPaidInvoicesAsync(string archivedBy);

    /// <summary>
    /// Haalt een gearchiveerde invoice op
    /// </summary>
    Task<Models.ArchivedInvoices?> GetArchivedInvoiceAsync(int archiveId);

    /// <summary>
    /// Haalt alle gearchiveerde invoices op voor een gebruiker
    /// </summary>
    Task<List<Models.ArchivedInvoices>> GetArchivedInvoicesForUserAsync(int userId);
}