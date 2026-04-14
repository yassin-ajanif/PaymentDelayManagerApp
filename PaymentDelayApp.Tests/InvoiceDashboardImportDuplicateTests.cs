using Microsoft.EntityFrameworkCore;
using Moq;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardImportDuplicateTests
{
    private static Invoice Inv(int supplierId, string number) =>
        new()
        {
            Id = 0,
            SupplierId = supplierId,
            InvoiceNumber = number,
            InvoiceDate = new DateOnly(2026, 1, 1),
            TtcAmount = 1m,
            EcheanceFactureJours = 30,
        };

    [TestMethod]
    public void AddInFileDuplicateErrors_EmptyParsed_NoChange()
    {
        var errors = new List<(int ExcelRow, string Message)>();
        InvoiceDashboardImportService.AddInFileDuplicateErrors([], errors);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void AddInFileDuplicateErrors_SingleRow_NoDuplicate()
    {
        var errors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<InvoiceDashboardImportParsedRow> { new(3, Inv(1, "A")) };
        InvoiceDashboardImportService.AddInFileDuplicateErrors(parsed, errors);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void AddInFileDuplicateErrors_SameSupplierAndNumber_SecondRowErrorReferencesFirstExcelRow()
    {
        var errors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<InvoiceDashboardImportParsedRow>
        {
            new(4, Inv(1, " X ")),
            new(9, Inv(1, "X")),
        };
        InvoiceDashboardImportService.AddInFileDuplicateErrors(parsed, errors);
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(9, errors[0].ExcelRow);
        Assert.IsTrue(errors[0].Message.Contains("ligne 4", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AddInFileDuplicateErrors_UnsortedParsed_FirstByExcelRowWins()
    {
        var errors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<InvoiceDashboardImportParsedRow>
        {
            new(10, Inv(1, "N")),
            new(5, Inv(1, "N")),
        };
        InvoiceDashboardImportService.AddInFileDuplicateErrors(parsed, errors);
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(10, errors[0].ExcelRow);
        Assert.IsTrue(errors[0].Message.Contains("ligne 5", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AddInFileDuplicateErrors_SameNumberDifferentSuppliers_NoInFileDuplicate()
    {
        var errors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<InvoiceDashboardImportParsedRow>
        {
            new(2, Inv(1, "N")),
            new(3, Inv(2, "N")),
        };
        InvoiceDashboardImportService.AddInFileDuplicateErrors(parsed, errors);
        Assert.AreEqual(0, errors.Count);
    }

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
    public async Task AddDatabaseDuplicateErrorsAsync_NoneExist_NoErrors()
    {
        var access = new Mock<IInvoiceAccess>();
        access.Setup(a => a.ExistsWithSupplierAndNumberAsync(It.IsAny<int>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            access.Object,
            db);
        var parsed = new List<InvoiceDashboardImportParsedRow> { new(2, Inv(1, "A")) };
        var errors = new List<(int ExcelRow, string Message)>();
        await sut.AddDatabaseDuplicateErrorsAsync(parsed, errors, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public async Task AddDatabaseDuplicateErrorsAsync_OneExists_AddsRowError()
    {
        var access = new Mock<IInvoiceAccess>();
        access.Setup(a => a.ExistsWithSupplierAndNumberAsync(1, "A", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            access.Object,
            db);
        var parsed = new List<InvoiceDashboardImportParsedRow> { new(7, Inv(1, "A")) };
        var errors = new List<(int ExcelRow, string Message)>();
        await sut.AddDatabaseDuplicateErrorsAsync(parsed, errors, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(7, errors[0].ExcelRow);
        Assert.IsTrue(errors[0].Message.Contains("déjà", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AddDatabaseDuplicateErrorsAsync_CancelledToken_PropagatesOperationCanceled()
    {
        var access = new Mock<IInvoiceAccess>();
        access.Setup(a => a.ExistsWithSupplierAndNumberAsync(It.IsAny<int>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Returns((int _, string _, int? _, CancellationToken ct) =>
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                return Task.FromResult(false);
            });
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            access.Object,
            db);
        var parsed = new List<InvoiceDashboardImportParsedRow> { new(1, Inv(1, "A")) };
        var errors = new List<(int ExcelRow, string Message)>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            sut.AddDatabaseDuplicateErrorsAsync(parsed, errors, cts.Token)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AddDatabaseDuplicateErrorsAsync_AccessThrows_Propagates()
    {
        var access = new Mock<IInvoiceAccess>();
        access.Setup(a => a.ExistsWithSupplierAndNumberAsync(It.IsAny<int>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            access.Object,
            db);
        var parsed = new List<InvoiceDashboardImportParsedRow> { new(1, Inv(1, "A")) };
        var errors = new List<(int ExcelRow, string Message)>();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.AddDatabaseDuplicateErrorsAsync(parsed, errors, CancellationToken.None)).ConfigureAwait(false);
    }
}
