using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Services;

public class InvoiceService : IInvoiceService
{
    private const int MaxEcheanceFactureJours = 120;

    private readonly IInvoiceAccess _invoices;
    private readonly ISupplierAccess _suppliers;

    public InvoiceService(IInvoiceAccess invoices, ISupplierAccess suppliers)
    {
        _invoices = invoices;
        _suppliers = suppliers;
    }

    public Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default) =>
        _invoices.GetAllAsync(cancellationToken);

    public Task<Invoice?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default) =>
        _invoices.GetByIdAsync(id, cancellationToken);

    public async Task SaveInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.InvoiceNumber = invoice.InvoiceNumber?.Trim() ?? string.Empty;

        if (invoice.EcheanceFactureJours is int ej && ej > MaxEcheanceFactureJours)
            throw new InvalidOperationException(
                $"La date d'échéance/facture ne peut pas dépasser {MaxEcheanceFactureJours} jours.");

        if (invoice.DeliveryOrServiceDate is { } del && del < invoice.InvoiceDate)
            throw new InvalidOperationException(
                "La date de livraison ou prestation doit être le même jour ou après la date de facture.");

        if (invoice.TtcAmount < 1m)
            throw new InvalidOperationException("Le TTC doit être supérieur ou égal à 1.");

        int? excludeId = invoice.Id == 0 ? null : invoice.Id;
        if (await _invoices.ExistsWithSupplierAndNumberAsync(
                invoice.SupplierId,
                invoice.InvoiceNumber,
                excludeId,
                cancellationToken))
            throw new InvalidOperationException("Ce numéro de facture existe déjà pour ce fournisseur.");

        var supplier = await _suppliers.GetByIdAsync(invoice.SupplierId, cancellationToken)
            ?? throw new InvalidOperationException("Fournisseur introuvable.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        PaymentAlertEvaluator.ApplyPaymentAlertFlag(invoice, supplier, today);

        if (invoice.Id == 0)
            await _invoices.AddAsync(invoice, cancellationToken);
        else
            await _invoices.UpdateAsync(invoice, cancellationToken);
    }

    public Task DeleteInvoiceAsync(int id, CancellationToken cancellationToken = default) =>
        _invoices.DeleteAsync(id, cancellationToken);

    public async Task SettleInvoiceAsync(int id, DateTime paidAt, CancellationToken cancellationToken = default)
    {
        var existing = await _invoices.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Facture introuvable.");
        var supplier = existing.Supplier
            ?? await _suppliers.GetByIdAsync(existing.SupplierId, cancellationToken)
            ?? throw new InvalidOperationException("Fournisseur introuvable.");

        existing.IsSettled = true;
        existing.PaidAt = paidAt;
        var today = DateOnly.FromDateTime(DateTime.Today);
        PaymentAlertEvaluator.ApplyPaymentAlertFlag(existing, supplier, today);
        await _invoices.UpdateAsync(existing, cancellationToken);
    }

    public async Task UnsettleInvoiceAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _invoices.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Facture introuvable.");
        if (!existing.IsSettled)
            return;
        var supplier = existing.Supplier
            ?? await _suppliers.GetByIdAsync(existing.SupplierId, cancellationToken)
            ?? throw new InvalidOperationException("Fournisseur introuvable.");

        existing.IsSettled = false;
        existing.PaidAt = null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        PaymentAlertEvaluator.ApplyPaymentAlertFlag(existing, supplier, today);
        await _invoices.UpdateAsync(existing, cancellationToken);
    }

    public async Task RefreshPaymentAlertFlagsAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await _invoices.GetAllAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var flags = new Dictionary<int, bool>();
        foreach (var inv in invoices)
        {
            if (inv.Supplier is null)
                continue;
            flags[inv.Id] = PaymentAlertEvaluator.IsPaymentAlert(inv, inv.Supplier, today);
        }

        if (flags.Count > 0)
            await _invoices.UpdatePaymentAlertFlagsAsync(flags, cancellationToken);
    }

    public async Task<int> CountUnsettledPaymentAlertsAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await _invoices.GetAllAsync(cancellationToken);
        return invoices.Count(i => i.IsPaymentAlert && !i.IsSettled);
    }
}
