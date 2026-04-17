using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Services;

public class SupplierService : ISupplierService
{
    private const int MinAlertSeuilJours = 1;
    private const int MaxAlertSeuilJours = 120;

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
        ArgumentNullException.ThrowIfNull(supplier);

        NormalizeSupplierStrings(supplier);

        if (string.IsNullOrEmpty(supplier.Name))
            throw new InvalidOperationException("Le nom du fournisseur est obligatoire.");

        if (supplier.AlertSeuilJours is < MinAlertSeuilJours or > MaxAlertSeuilJours)
            throw new InvalidOperationException(
                $"Le seuil d'alerte doit être entre {MinAlertSeuilJours} et {MaxAlertSeuilJours} jours.");

        int? excludeId = supplier.Id == 0 ? null : supplier.Id;

        if (await _suppliers.NameExistsAsync(supplier.Name, excludeId, cancellationToken))
            throw new InvalidOperationException("Un autre fournisseur porte déjà ce nom.");

        if (supplier.Ice is not null &&
            await _suppliers.IceExistsAsync(supplier.Ice, excludeId, cancellationToken))
            throw new InvalidOperationException("Un autre fournisseur utilise déjà cet ICE.");

        if (supplier.FiscalId is not null &&
            await _suppliers.FiscalIdExistsAsync(supplier.FiscalId, excludeId, cancellationToken))
            throw new InvalidOperationException("Un autre fournisseur utilise déjà cet IF.");

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

    private static void NormalizeSupplierStrings(Supplier supplier)
    {
        supplier.Name = supplier.Name?.Trim() ?? string.Empty;
        supplier.Ice = string.IsNullOrWhiteSpace(supplier.Ice) ? null : supplier.Ice.Trim();
        supplier.FiscalId = string.IsNullOrWhiteSpace(supplier.FiscalId) ? null : supplier.FiscalId.Trim();
        supplier.Address = string.IsNullOrWhiteSpace(supplier.Address) ? null : supplier.Address.Trim();
        supplier.Activite = string.IsNullOrWhiteSpace(supplier.Activite) ? null : supplier.Activite.Trim();
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
