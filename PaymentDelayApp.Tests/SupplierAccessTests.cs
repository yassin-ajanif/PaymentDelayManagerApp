using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DataAccessLayer.Access;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class SupplierAccessTests
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
    public async Task NameExistsAsync_IsCaseInsensitive()
    {
        await using var db = CreateSqliteDb();
        db.Suppliers.Add(new Supplier { Name = "acme" });
        await db.SaveChangesAsync();

        var sut = new SupplierAccess(db);
        var exists = await sut.NameExistsAsync("ACME", null);

        Assert.IsTrue(exists);
    }

    [TestMethod]
    public async Task IceExistsAsync_IsCaseSensitive_CurrentBehavior()
    {
        await using var db = CreateSqliteDb();
        db.Suppliers.Add(new Supplier { Name = "A", Ice = "ab12" });
        await db.SaveChangesAsync();

        var sut = new SupplierAccess(db);

        Assert.IsTrue(await sut.IceExistsAsync("ab12", null));
        Assert.IsFalse(await sut.IceExistsAsync("AB12", null));
    }

    [TestMethod]
    public async Task FiscalIdExistsAsync_IsCaseSensitive_CurrentBehavior()
    {
        await using var db = CreateSqliteDb();
        db.Suppliers.Add(new Supplier { Name = "A", FiscalId = "if9" });
        await db.SaveChangesAsync();

        var sut = new SupplierAccess(db);

        Assert.IsTrue(await sut.FiscalIdExistsAsync("if9", null));
        Assert.IsFalse(await sut.FiscalIdExistsAsync("IF9", null));
    }

    [TestMethod]
    public async Task ExistsChecks_RespectExcludeId()
    {
        await using var db = CreateSqliteDb();
        var supplier = new Supplier { Name = "Acme", Ice = "X1", FiscalId = "F1" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var sut = new SupplierAccess(db);

        Assert.IsFalse(await sut.NameExistsAsync("acme", supplier.Id));
        Assert.IsFalse(await sut.IceExistsAsync("X1", supplier.Id));
        Assert.IsFalse(await sut.FiscalIdExistsAsync("F1", supplier.Id));
    }
}
