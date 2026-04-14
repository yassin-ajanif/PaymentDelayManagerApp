using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardImportFromExcelTests
{
    private static string Fr(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

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

    private static IReadOnlyList<Supplier> SingleAcme() =>
        new List<Supplier> { new() { Id = 1, Name = "Acme" } };

    private static string[] ValidNine(string invoiceNo, DateOnly invD, int jours)
    {
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, jours);
        return
        [
            Fr(invD),
            "",
            invoiceNo,
            "Acme",
            "",
            "10,00",
            Fr(lim),
            "1 j",
            Fr(lim),
        ];
    }

    /// <summary>Minimal valid SpreadsheetML package with no worksheets (OPC zip).</summary>
    private static MemoryStream CreateXlsxStreamWithNoWorksheets()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            static void Add(ZipArchive z, string name, string utf8Xml)
            {
                var e = z.CreateEntry(name, CompressionLevel.Fastest);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(utf8Xml);
            }

            Add(zip, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets/>
                </workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
                """);
        }

        ms.Position = 0;
        return ms;
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_CancelledToken_ThrowsOperationCanceled()
    {
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            Mock.Of<IInvoiceAccess>(),
            db);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var ct = new CancellationToken(true);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            sut.ImportFromExcelAsync(stream, ct)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_InvalidStream_Throws()
    {
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = new MemoryStream([1, 2, 3, 4]);
        await Assert.ThrowsExceptionAsync<FileFormatException>(() =>
            sut.ImportFromExcelAsync(stream, CancellationToken.None)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_NoWorksheets_HeaderFailureMissingSheet()
    {
        using var db = CreateSqliteDb();
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            Mock.Of<ISupplierService>(),
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = CreateXlsxStreamWithNoWorksheets();
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.MissingHeaders.Any(h => h.Contains("Feuille introuvable", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_HeaderNotFound_HeaderFailure()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(1, 1).Value = "not a header";
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.MissingHeaders.Count > 0);
        mockSup.Verify(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_OnlyHeaderNoData_OkZero()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.ImportedCount);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_ParseError_NoSave()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ["not-a-date", "", "1", "Acme", "", "1", "", "", ""]);
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, result.RowErrors.Count);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_MixedValidAndInvalid_NoSave()
    {
        var y = DateTime.Today.Year;
        var invD = new DateOnly(y, 3, 10);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ValidNine("G1", invD, 25));
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 3, ["bad", "", "2", "Acme", "", "1", "", "", ""]);
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_InFileDuplicate_NoSave()
    {
        var y = DateTime.Today.Year;
        var invD = new DateOnly(y, 3, 10);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ValidNine("DUP", invD, 25));
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 3, ValidNine("DUP", invD, 25));
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.RowErrors.Any(e => e.ExcelRow == 3));
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_DatabaseDuplicate_NoSave()
    {
        var y = DateTime.Today.Year;
        var invD = new DateOnly(y, 3, 10);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ValidNine("DB1", invD, 25));
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockAcc = new Mock<IInvoiceAccess>();
        mockAcc.Setup(a => a.ExistsWithSupplierAndNumberAsync(1, "DB1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            mockAcc.Object,
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsFalse(result.Success);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_TwoValidRows_SavesInOrder()
    {
        var y = DateTime.Today.Year;
        var invD = new DateOnly(y, 5, 6);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ValidNine("A1", invD, 30));
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 3, ValidNine("A2", invD, 30));
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockAcc = new Mock<IInvoiceAccess>();
        mockAcc.Setup(a => a.ExistsWithSupplierAndNumberAsync(It.IsAny<int>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var mockInv = new Mock<IInvoiceService>();
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            mockAcc.Object,
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        var result = await sut.ImportFromExcelAsync(stream, CancellationToken.None).ConfigureAwait(false);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ImportedCount);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockInv.Verify(i => i.SaveInvoiceAsync(It.Is<Invoice>(x => x.InvoiceNumber == "A1"), It.IsAny<CancellationToken>()), Times.Once);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.Is<Invoice>(x => x.InvoiceNumber == "A2"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_SecondSaveThrows_ExceptionPropagates()
    {
        var y = DateTime.Today.Year;
        var invD = new DateOnly(y, 5, 6);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ValidNine("S1", invD, 30));
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 3, ValidNine("S2", invD, 30));
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SingleAcme());
        var mockAcc = new Mock<IInvoiceAccess>();
        mockAcc.Setup(a => a.ExistsWithSupplierAndNumberAsync(It.IsAny<int>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var mockInv = new Mock<IInvoiceService>();
        mockInv.SetupSequence(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .ThrowsAsync(new InvalidOperationException("save failed"));
        var sut = new InvoiceDashboardImportService(
            mockInv.Object,
            mockSup.Object,
            mockAcc.Object,
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            sut.ImportFromExcelAsync(stream, CancellationToken.None)).ConfigureAwait(false);
        mockInv.Verify(i => i.SaveInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ImportFromExcelAsync_GetSuppliersThrows_Propagates()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        InvoiceDashboardImportTestExcel.WriteHeaderRow(ws, 1);
        using var db = CreateSqliteDb();
        var mockSup = new Mock<ISupplierService>();
        mockSup.Setup(s => s.GetSuppliersAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new FormatException("suppliers"));
        var sut = new InvoiceDashboardImportService(
            Mock.Of<IInvoiceService>(),
            mockSup.Object,
            Mock.Of<IInvoiceAccess>(),
            db);
        using var stream = InvoiceDashboardImportTestExcel.WorkbookToRewindableStream(wb);
        await Assert.ThrowsExceptionAsync<FormatException>(() =>
            sut.ImportFromExcelAsync(stream, CancellationToken.None)).ConfigureAwait(false);
    }
}
