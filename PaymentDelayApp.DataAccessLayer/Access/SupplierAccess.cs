using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.DataAccessLayer.Access;

public class SupplierAccess : ISupplierAccess
{
    private readonly PaymentDelayDbContext _db;

    public SupplierAccess(PaymentDelayDbContext db) => _db = db;

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public async Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Suppliers.FindAsync([supplier.Id], cancellationToken)
            ?? throw new InvalidOperationException($"Fournisseur introuvable (Id = {supplier.Id}).");

        entity.Name = supplier.Name;
        entity.Ice = supplier.Ice;
        entity.FiscalId = supplier.FiscalId;
        entity.Address = supplier.Address;
        entity.Activite = supplier.Activite;
        entity.AlertSeuilJours = supplier.AlertSeuilJours;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Suppliers.FindAsync([id], cancellationToken);
        if (entity is null)
            return;
        _db.Suppliers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
