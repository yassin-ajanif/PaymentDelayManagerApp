using Moq;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.BusinessLayer.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class SupplierServiceTests
{
    [TestMethod]
    public async Task SaveSupplierAsync_NameRequired_Throws()
    {
        var suppliers = new Mock<ISupplierAccess>();
        var invoices = new Mock<IInvoiceAccess>();
        var sut = new SupplierService(suppliers.Object, invoices.Object);

        var supplier = new Supplier { Name = "   " };

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveSupplierAsync(supplier));
    }

    [TestMethod]
    public async Task SaveSupplierAsync_TrimFields_BeforeAdd()
    {
        var suppliers = new Mock<ISupplierAccess>();
        suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.IceExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.FiscalIdExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Supplier? captured = null;
        suppliers.Setup(x => x.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

        await sut.SaveSupplierAsync(new Supplier
        {
            Name = "  ACME  ",
            Ice = "  ICE1  ",
            FiscalId = "  IF1  ",
            Address = "  Rue test  ",
            Activite = "  service  ",
        });

        Assert.IsNotNull(captured);
        Assert.AreEqual("ACME", captured!.Name);
        Assert.AreEqual("ICE1", captured.Ice);
        Assert.AreEqual("IF1", captured.FiscalId);
        Assert.AreEqual("  Rue test  ", captured.Address);
        Assert.AreEqual("  service  ", captured.Activite);
    }

    [TestMethod]
    public async Task SaveSupplierAsync_DuplicateName_Throws()
    {
        var suppliers = new Mock<ISupplierAccess>();
        suppliers.Setup(x => x.NameExistsAsync("acme", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveSupplierAsync(new Supplier { Name = "acme" }));
    }

    [TestMethod]
    public async Task SaveSupplierAsync_DuplicateIce_Throws()
    {
        var suppliers = new Mock<ISupplierAccess>();
        suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.IceExistsAsync("ab12", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveSupplierAsync(new Supplier { Name = "A", Ice = "ab12" }));
    }

    [TestMethod]
    public async Task SaveSupplierAsync_DuplicateFiscalId_Throws()
    {
        var suppliers = new Mock<ISupplierAccess>();
        suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.IceExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.FiscalIdExistsAsync("if9", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveSupplierAsync(new Supplier { Name = "A", FiscalId = "if9" }));
    }

    [TestMethod]
    public async Task SaveSupplierAsync_Edit_UsesExcludeId()
    {
        var suppliers = new Mock<ISupplierAccess>();
        suppliers.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Supplier { Id = 7, Name = "old", AlertSeuilJours = 7 });

        suppliers.Setup(x => x.NameExistsAsync("ACME", 7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.IceExistsAsync("ICE1", 7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.FiscalIdExistsAsync("IF1", 7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        suppliers.Setup(x => x.UpdateAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

        await sut.SaveSupplierAsync(new Supplier
        {
            Id = 7,
            Name = "ACME",
            Ice = "ICE1",
            FiscalId = "IF1",
            AlertSeuilJours = 7,
        });

        suppliers.Verify(x => x.NameExistsAsync("ACME", 7, It.IsAny<CancellationToken>()), Times.Once);
        suppliers.Verify(x => x.IceExistsAsync("ICE1", 7, It.IsAny<CancellationToken>()), Times.Once);
        suppliers.Verify(x => x.FiscalIdExistsAsync("IF1", 7, It.IsAny<CancellationToken>()), Times.Once);
    }

[TestMethod]
public async Task SaveSupplierAsync_Create_DuplicateIce_DifferentCase_IsDetectedAsDuplicate()
{
    var suppliers = new Mock<ISupplierAccess>();
    suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Existing value in storage is "ab12"; entered value is "AB12" -> duplicate must be detected.
    suppliers.Setup(x => x.IceExistsAsync("ab12", null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Name = "A", Ice = "AB12" }));
}

[TestMethod]
public async Task SaveSupplierAsync_Edit_DuplicateIce_DifferentCase_IsDetectedAsDuplicate()
{
    var suppliers = new Mock<ISupplierAccess>();
    suppliers.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Supplier { Id = 7, Name = "Old", AlertSeuilJours = 7 });

    suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), 7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Existing value in storage is "ab12"; edited value is "AB12" -> duplicate must be detected.
    suppliers.Setup(x => x.IceExistsAsync("ab12", 7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Id = 7, Name = "A", Ice = "AB12" }));
}

[TestMethod]
public async Task SaveSupplierAsync_Create_DuplicateFiscalId_DifferentCase_IsDetectedAsDuplicate()
{
    var suppliers = new Mock<ISupplierAccess>();
    suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    suppliers.Setup(x => x.IceExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Existing value in storage is "ab12"; entered value is "AB12" -> duplicate must be detected.
    suppliers.Setup(x => x.FiscalIdExistsAsync("ab12", null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Name = "A", FiscalId = "AB12" }));
}

[TestMethod]
public async Task SaveSupplierAsync_Edit_DuplicateFiscalId_DifferentCase_IsDetectedAsDuplicate()
{
    var suppliers = new Mock<ISupplierAccess>();
    suppliers.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Supplier { Id = 7, Name = "Old", AlertSeuilJours = 7 });

    suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), 7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    suppliers.Setup(x => x.IceExistsAsync(It.IsAny<string>(), 7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Existing value in storage is "ab12"; edited value is "AB12" -> duplicate must be detected.
    suppliers.Setup(x => x.FiscalIdExistsAsync("ab12", 7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Id = 7, Name = "A", FiscalId = "AB12" }));
}

[TestMethod]
public async Task SaveSupplierAsync_AddressCase_IsPreserved()
{
    var suppliers = new Mock<ISupplierAccess>();
    suppliers.Setup(x => x.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    suppliers.Setup(x => x.IceExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    suppliers.Setup(x => x.FiscalIdExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    Supplier? captured = null;
    suppliers.Setup(x => x.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
        .Callback<Supplier, CancellationToken>((s, _) => captured = s)
        .Returns(Task.CompletedTask);

    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await sut.SaveSupplierAsync(new Supplier
    {
        Name = "A",
        Address = "RUE TEST",
        Activite = "SERVICE"
    });

    Assert.IsNotNull(captured);
    Assert.AreEqual("RUE TEST", captured!.Address);
    Assert.AreEqual("SERVICE", captured.Activite);
}

[TestMethod]
public async Task SaveSupplierAsync_AlertSeuilJours_BelowMinimum_Throws()
{
    var suppliers = new Mock<ISupplierAccess>();
    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Name = "A", AlertSeuilJours = 0 }));
}

[TestMethod]
public async Task SaveSupplierAsync_AlertSeuilJours_AboveMaximum_Throws()
{
    var suppliers = new Mock<ISupplierAccess>();
    var sut = new SupplierService(suppliers.Object, Mock.Of<IInvoiceAccess>());

    await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
        sut.SaveSupplierAsync(new Supplier { Name = "A", AlertSeuilJours = 121 }));
}
}
