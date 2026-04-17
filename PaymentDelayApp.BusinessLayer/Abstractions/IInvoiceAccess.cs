using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Abstractions;

public interface IInvoiceAccess
{
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasAnyForSupplierAsync(int supplierId, CancellationToken cancellationToken = default);
    Task UpdatePaymentAlertFlagsAsync(
        IReadOnlyDictionary<int, bool> flagsByInvoiceId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsWithSupplierAndNumberAsync(
        int supplierId,
        string invoiceNumber,
        int? excludeInvoiceId,
        CancellationToken cancellationToken = default);
}
