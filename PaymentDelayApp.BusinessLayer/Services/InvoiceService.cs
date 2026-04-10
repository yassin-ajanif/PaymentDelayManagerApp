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

    public Task<IReadOnlyList<Invoice>> GetAlertInvoicesAsync(CancellationToken cancellationToken = default) =>
        _invoices.GetAlertCandidatesAsync(cancellationToken);

    public async Task SaveInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.EcheanceFactureJours > MaxEcheanceFactureJours)
            throw new ArgumentOutOfRangeException(nameof(invoice), invoice.EcheanceFactureJours,
                $"La date d'échéance/facture ne peut pas dépasser {MaxEcheanceFactureJours} jours.");

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
}
