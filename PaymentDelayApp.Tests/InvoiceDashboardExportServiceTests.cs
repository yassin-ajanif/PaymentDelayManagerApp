using System.IO;
using ClosedXML.Excel;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardExportServiceTests
{
    private static InvoiceDashboardRow Row(
        string invoiceNumber = "N-1",
        string supplier = "Supplier",
        string? designation = "Desc",
        DateOnly? delivery = null) =>
        new()
        {
            Id = 1,
            InvoiceDate = new DateOnly(2026, 3, 10),
            DeliveryOrServiceDate = delivery,
            InvoiceNumber = invoiceNumber,
            SupplierName = supplier,
            Designation = designation,
            TtcAmount = 123.45m,
            EcheanceFactureJours = 30,
            EcheanceNormaleJours = -5,
            EcheanceRespecteeJours = 30,
            ResteDesJours = 12,
            AlertSeuilJours = 7,
            IsSettled = false,
        };

    [TestMethod]
    public async Task WriteExcelAsync_WritesTitleStampHeadersAndRows()
    {
        var sut = new InvoiceDashboardExportService();
        var rows = new List<InvoiceDashboardRow> { Row(), Row("N-2", "Other", null) };
        using var ms = new MemoryStream();
        await sut.WriteExcelAsync(rows, ms, "Rapport test", "Export — 2026-04-13 10:00", CancellationToken.None).ConfigureAwait(false);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();
        Assert.AreEqual("Rapport test", ws.Cell(1, 1).GetString());
        Assert.AreEqual("Export — 2026-04-13 10:00", ws.Cell(2, 1).GetString());
        var headerRow = DashboardInvoiceExcelLayout.HeaderRowNumber;
        for (var c = 0; c < DashboardInvoiceExcelLayout.ColumnCount; c++)
            Assert.AreEqual(DashboardInvoiceExcelLayout.ColumnHeaders[c], ws.Cell(headerRow, c + 1).GetString());
        var dataStart = DashboardInvoiceExcelLayout.FirstDataRowNumber;
        Assert.AreEqual(rows[0].DateFactureDisplay, ws.Cell(dataStart, 1).GetString());
        Assert.AreEqual(rows[0].DateLivraisonDisplay, ws.Cell(dataStart, 2).GetString());
        Assert.AreEqual("N-1", ws.Cell(dataStart, 3).GetString());
        Assert.AreEqual("Supplier", ws.Cell(dataStart, 4).GetString());
        Assert.AreEqual("Desc", ws.Cell(dataStart, 5).GetString());
        Assert.AreEqual(rows[0].TtcDisplay, ws.Cell(dataStart, 6).GetString());
        Assert.AreEqual(rows[1].InvoiceNumber, ws.Cell(dataStart + 1, 3).GetString());
        Assert.AreEqual(string.Empty, ws.Cell(dataStart + 1, 5).GetString());
        Assert.AreEqual("Rapport test", wb.Properties.Title);
    }

    [TestMethod]
    public async Task WriteExcelAsync_EmptyRows_StillWritesLayout()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await sut.WriteExcelAsync([], ms, "Titre", "Stamp", CancellationToken.None).ConfigureAwait(false);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();
        Assert.AreEqual("Titre", ws.Cell(1, 1).GetString());
        Assert.AreEqual("Stamp", ws.Cell(2, 1).GetString());
        var headerRow = DashboardInvoiceExcelLayout.HeaderRowNumber;
        Assert.AreEqual(DashboardInvoiceExcelLayout.ColumnHeaders[0], ws.Cell(headerRow, 1).GetString());
    }

    [TestMethod]
    public async Task WriteExcelAsync_SanitizesInvalidSheetCharacters()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await sut.WriteExcelAsync([Row()], ms, "A/B*C?D:E[F]", "s", CancellationToken.None).ConfigureAwait(false);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var name = wb.Worksheets.First().Name;
        Assert.IsFalse(name.Contains('/', StringComparison.Ordinal));
        Assert.IsFalse(name.Contains('*', StringComparison.Ordinal));
        Assert.IsTrue(name.Length <= 31);
    }

    [TestMethod]
    public async Task WriteExcelAsync_OnlyInvalidTitleCharacters_UsesDefaultSheetName()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await sut.WriteExcelAsync([Row()], ms, "///", "s", CancellationToken.None).ConfigureAwait(false);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        Assert.AreEqual("Factures", wb.Worksheets.First().Name);
    }

    [TestMethod]
    public async Task WriteExcelAsync_LongTitle_TruncatesSheetNameTo31()
    {
        var sut = new InvoiceDashboardExportService();
        var longTitle = new string('X', 40);
        using var ms = new MemoryStream();
        await sut.WriteExcelAsync([Row()], ms, longTitle, "s", CancellationToken.None).ConfigureAwait(false);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        Assert.AreEqual(31, wb.Worksheets.First().Name.Length);
        Assert.IsTrue(wb.Worksheets.First().Name.StartsWith("XXX", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WriteExcelAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            sut.WriteExcelAsync([Row()], ms, "T", "s", new CancellationToken(true))).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task WritePdfAsync_WritesPdfHeader()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await sut.WritePdfAsync([Row()], ms, "PDF titre", "PDF stamp", CancellationToken.None).ConfigureAwait(false);
        var bytes = ms.ToArray();
        Assert.IsTrue(bytes.Length > 200, "Expected non-trivial PDF output.");
        CollectionAssert.AreEqual(new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' }, bytes.Take(4).ToArray());
    }

    [TestMethod]
    public async Task WritePdfAsync_EmptyRows_StillProducesPdf()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await sut.WritePdfAsync([], ms, "T", "s", CancellationToken.None).ConfigureAwait(false);
        var bytes = ms.ToArray();
        Assert.AreEqual('%', (char)bytes[0]);
    }

    [TestMethod]
    public async Task WritePdfAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        var sut = new InvoiceDashboardExportService();
        using var ms = new MemoryStream();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            sut.WritePdfAsync([Row()], ms, "T", "s", new CancellationToken(true))).ConfigureAwait(false);
    }
}
