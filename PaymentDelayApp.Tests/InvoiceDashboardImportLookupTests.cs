using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardImportLookupTests
{
    [TestMethod]
    public void BuildSupplierLookup_Empty_NoAmbiguous()
    {
        var map = InvoiceDashboardImportService.BuildSupplierLookup([], out var amb);
        Assert.AreEqual(0, map.Count);
        Assert.AreEqual(0, amb.Count);
    }

    [TestMethod]
    public void BuildSupplierLookup_OneSupplier_MapsByNormalizedName()
    {
        var s = new Supplier { Id = 1, Name = "  Acme  " };
        var map = InvoiceDashboardImportService.BuildSupplierLookup([s], out var amb);
        Assert.AreEqual(0, amb.Count);
        Assert.AreEqual(1, map.Count);
        Assert.IsTrue(map.TryGetValue("Acme", out var got));
        Assert.AreSame(s, got);
    }

    [TestMethod]
    public void BuildSupplierLookup_TwoSuppliersSameNameCaseInsensitive_BothAmbiguousAndRemoved()
    {
        var a = new Supplier { Id = 1, Name = "Acme" };
        var b = new Supplier { Id = 2, Name = "acme" };
        var map = InvoiceDashboardImportService.BuildSupplierLookup([a, b], out var amb);
        Assert.IsTrue(amb.Count > 0);
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void BuildSupplierLookup_TwoSuppliersIdenticalTrimmedName_BothRemoved()
    {
        var a = new Supplier { Id = 1, Name = "Foo" };
        var b = new Supplier { Id = 2, Name = "Foo" };
        var map = InvoiceDashboardImportService.BuildSupplierLookup([a, b], out var amb);
        Assert.IsTrue(amb.Contains("Foo"));
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void BuildSupplierLookup_WhitespaceOnlyName_Skipped()
    {
        var a = new Supplier { Id = 1, Name = "   " };
        var b = new Supplier { Id = 2, Name = "Real" };
        var map = InvoiceDashboardImportService.BuildSupplierLookup([a, b], out _);
        Assert.AreEqual(1, map.Count);
        Assert.IsTrue(map.ContainsKey("Real"));
    }

    [TestMethod]
    public void BuildSupplierLookup_ThreeSuppliersA_a_B_OnlyBRemains()
    {
        var a = new Supplier { Id = 1, Name = "A" };
        var a2 = new Supplier { Id = 2, Name = "a" };
        var b = new Supplier { Id = 3, Name = "B" };
        var map = InvoiceDashboardImportService.BuildSupplierLookup([a, a2, b], out var amb);
        Assert.AreEqual(1, map.Count);
        Assert.IsTrue(map.ContainsKey("B"));
        Assert.IsFalse(map.ContainsKey("A"));
        Assert.IsTrue(amb.Count > 0);
    }
}
