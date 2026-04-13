using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.Services;

public interface ISupplierExcelService
{
    Task WriteExcelAsync(
        IReadOnlyList<Supplier> suppliers,
        Stream destination,
        string reportTitle,
        string exportTimestampLine,
        CancellationToken cancellationToken = default);

    Task<SupplierExcelImportResult> ImportFromExcelAsync(Stream stream, CancellationToken cancellationToken = default);
}
