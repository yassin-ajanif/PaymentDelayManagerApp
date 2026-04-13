namespace PaymentDelayApp.Services;

public interface IInvoiceDashboardImportService
{
    /// <summary>
    /// Validates and imports invoices from a stream in dashboard export layout.
    /// All-or-nothing: on any validation error, no rows are persisted.
    /// </summary>
    Task<InvoiceDashboardImportResult> ImportFromExcelAsync(Stream stream, CancellationToken cancellationToken = default);
}
