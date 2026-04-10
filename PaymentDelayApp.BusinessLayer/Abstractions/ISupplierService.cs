using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Abstractions;

public interface ISupplierService
{
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(int id, CancellationToken cancellationToken = default);
    Task SaveSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task DeleteSupplierAsync(int id, CancellationToken cancellationToken = default);
}
