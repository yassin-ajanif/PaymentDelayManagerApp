using PaymentDelayApp.Models;

namespace PaymentDelayApp.Services;

public interface IInvoiceDashboardExportService
{
    Task WritePdfAsync(
        IReadOnlyList<InvoiceDashboardRow> rows,
        Stream destination,
        string reportTitle,
        string exportTimestampLine,
        CancellationToken cancellationToken = default);

    Task WriteExcelAsync(
        IReadOnlyList<InvoiceDashboardRow> rows,
        Stream destination,
        string reportTitle,
        string exportTimestampLine,
        CancellationToken cancellationToken = default);
}
