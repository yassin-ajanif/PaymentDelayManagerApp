using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.DataAccessLayer.Access;

public class InvoiceAccess : IInvoiceAccess
{
    private readonly PaymentDelayDbContext _db;

    public InvoiceAccess(PaymentDelayDbContext db) => _db = db;

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetAlertCandidatesAsync(CancellationToken cancellationToken = default) =>
        await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.IsPaymentAlert && !i.IsSettled)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Invoices.FindAsync([invoice.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Facture introuvable (Id = {invoice.Id}).");

        entity.SupplierId = invoice.SupplierId;
        entity.InvoiceDate = invoice.InvoiceDate;
        entity.DeliveryOrServiceDate = invoice.DeliveryOrServiceDate;
        entity.InvoiceNumber = invoice.InvoiceNumber;
        entity.Designation = invoice.Designation;
        entity.TtcAmount = invoice.TtcAmount;
        entity.EcheanceFactureJours = invoice.EcheanceFactureJours;
        entity.IsSettled = invoice.IsSettled;
        entity.IsPaymentAlert = invoice.IsPaymentAlert;
        entity.PaidAt = invoice.PaidAt;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Invoices.FindAsync([id], cancellationToken);
        if (entity is null)
            return;
        _db.Invoices.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasAnyForSupplierAsync(int supplierId, CancellationToken cancellationToken = default) =>
        _db.Invoices.AnyAsync(i => i.SupplierId == supplierId, cancellationToken);

    public async Task UpdatePaymentAlertFlagsAsync(
        IReadOnlyDictionary<int, bool> flagsByInvoiceId,
        CancellationToken cancellationToken = default)
    {
        foreach (var (id, isAlert) in flagsByInvoiceId)
        {
            var entity = await _db.Invoices.FindAsync([id], cancellationToken);
            if (entity is not null)
                entity.IsPaymentAlert = isAlert;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
