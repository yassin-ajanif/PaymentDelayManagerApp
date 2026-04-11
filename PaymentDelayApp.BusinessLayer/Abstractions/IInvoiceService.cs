using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Abstractions;

public interface IInvoiceService
{
    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default);
    Task<Invoice?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default);
    Task SaveInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task DeleteInvoiceAsync(int id, CancellationToken cancellationToken = default);
    Task SettleInvoiceAsync(int id, DateTime paidAt, CancellationToken cancellationToken = default);
    Task UnsettleInvoiceAsync(int id, CancellationToken cancellationToken = default);
    Task RefreshPaymentAlertFlagsAsync(CancellationToken cancellationToken = default);
}
