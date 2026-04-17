using Moq;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.BusinessLayer.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceServiceTests
{
    [TestMethod]
    public async Task SaveInvoiceAsync_DeliveryBeforeInvoice_Throws()
    {
        var inv = new Mock<IInvoiceAccess>();
        var sup = new Mock<ISupplierAccess>();
        var sut = new InvoiceService(inv.Object, sup.Object);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveInvoiceAsync(new Invoice
            {
                SupplierId = 1,
                InvoiceDate = new DateOnly(2026, 6, 10),
                DeliveryOrServiceDate = new DateOnly(2026, 6, 1),
                InvoiceNumber = "F1",
                TtcAmount = 10m,
                EcheanceFactureJours = 60,
            }));
    }

    [TestMethod]
    public async Task SaveInvoiceAsync_TtcBelowOne_Throws()
    {
        var inv = new Mock<IInvoiceAccess>();
        var sup = new Mock<ISupplierAccess>();
        var sut = new InvoiceService(inv.Object, sup.Object);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveInvoiceAsync(new Invoice
            {
                SupplierId = 1,
                InvoiceDate = new DateOnly(2026, 6, 10),
                InvoiceNumber = "F1",
                TtcAmount = 0.99m,
                EcheanceFactureJours = 60,
            }));
    }

    [TestMethod]
    public async Task SaveInvoiceAsync_DuplicateNumber_Throws()
    {
        var inv = new Mock<IInvoiceAccess>();
        inv.Setup(x => x.ExistsWithSupplierAndNumberAsync(1, "F1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sup = new Mock<ISupplierAccess>();
        var sut = new InvoiceService(inv.Object, sup.Object);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.SaveInvoiceAsync(new Invoice
            {
                SupplierId = 1,
                InvoiceDate = new DateOnly(2026, 6, 10),
                InvoiceNumber = "F1",
                TtcAmount = 10m,
                EcheanceFactureJours = 60,
            }));

        inv.Verify(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SaveInvoiceAsync_Create_CallsAdd()
    {
        var inv = new Mock<IInvoiceAccess>();
        inv.Setup(x => x.ExistsWithSupplierAndNumberAsync(1, "F1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        inv.Setup(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sup = new Mock<ISupplierAccess>();
        sup.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Supplier { Id = 1, Name = "A", AlertSeuilJours = 7 });

        var sut = new InvoiceService(inv.Object, sup.Object);

        await sut.SaveInvoiceAsync(new Invoice
        {
            Id = 0,
            SupplierId = 1,
            InvoiceDate = new DateOnly(2026, 6, 10),
            InvoiceNumber = "F1",
            TtcAmount = 1m,
            EcheanceFactureJours = 60,
        });

        inv.Verify(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
