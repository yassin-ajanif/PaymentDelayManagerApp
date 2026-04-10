using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierAccess _suppliers;
    private readonly IInvoiceAccess _invoices;

    public SupplierService(ISupplierAccess suppliers, IInvoiceAccess invoices)
    {
        _suppliers = suppliers;
        _invoices = invoices;
    }

    public Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default) =>
        _suppliers.GetAllAsync(cancellationToken);

    public Task<Supplier?> GetSupplierAsync(int id, CancellationToken cancellationToken = default) =>
        _suppliers.GetByIdAsync(id, cancellationToken);

    public async Task SaveSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        int? oldSeuil = null;
        if (supplier.Id != 0)
        {
            var previous = await _suppliers.GetByIdAsync(supplier.Id, cancellationToken);
            oldSeuil = previous?.AlertSeuilJours;
        }

        if (supplier.Id == 0)
            await _suppliers.AddAsync(supplier, cancellationToken);
        else
            await _suppliers.UpdateAsync(supplier, cancellationToken);

        if (oldSeuil.HasValue && oldSeuil.Value != supplier.AlertSeuilJours)
            await RefreshAlertsForSupplierAsync(supplier.Id, cancellationToken);
    }

    public async Task DeleteSupplierAsync(int id, CancellationToken cancellationToken = default)
    {
        if (await _invoices.HasAnyForSupplierAsync(id, cancellationToken))
            throw new InvalidOperationException(
                "Impossible de supprimer ce fournisseur : des factures y sont liées.");

        await _suppliers.DeleteAsync(id, cancellationToken);
    }

    private async Task RefreshAlertsForSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
            return;

        var all = await _invoices.GetAllAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var flags = new Dictionary<int, bool>();
        foreach (var inv in all.Where(i => i.SupplierId == supplierId))
        {
            inv.Supplier = supplier;
            flags[inv.Id] = PaymentAlertEvaluator.IsPaymentAlert(inv, supplier, today);
        }

        if (flags.Count > 0)
            await _invoices.UpdatePaymentAlertFlagsAsync(flags, cancellationToken);
    }
}
