using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DataAccessLayer.Access;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceAccessTests
{
    private static PaymentDelayDbContext CreateSqliteDb()
    {
        var db = new PaymentDelayDbContext(
            new DbContextOptionsBuilder<PaymentDelayDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options);

        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    [TestMethod]
    public async Task ExistsWithSupplierAndNumberAsync_IsCaseInsensitive()
    {
        await using var db = CreateSqliteDb();
        var supplier = new Supplier { Name = "Acme" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        db.Invoices.Add(new Invoice
        {
            SupplierId = supplier.Id,
            InvoiceDate = new DateOnly(2026, 4, 1),
            InvoiceNumber = "inv-42",
            TtcAmount = 100m,
            EcheanceFactureJours = 30,
        });
        await db.SaveChangesAsync();

        var sut = new InvoiceAccess(db);

        Assert.IsTrue(await sut.ExistsWithSupplierAndNumberAsync(supplier.Id, "INV-42", null));
        Assert.IsTrue(await sut.ExistsWithSupplierAndNumberAsync(supplier.Id, "Inv-42", null));
    }

    [TestMethod]
    public async Task ExistsWithSupplierAndNumberAsync_ExcludeId_IgnoresSameRow()
    {
        await using var db = CreateSqliteDb();
        var supplier = new Supplier { Name = "Acme" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var row = new Invoice
        {
            SupplierId = supplier.Id,
            InvoiceDate = new DateOnly(2026, 4, 1),
            InvoiceNumber = "x",
            TtcAmount = 1m,
            EcheanceFactureJours = 30,
        };
        db.Invoices.Add(row);
        await db.SaveChangesAsync();

        var sut = new InvoiceAccess(db);

        Assert.IsFalse(await sut.ExistsWithSupplierAndNumberAsync(supplier.Id, "X", row.Id));
    }
}
