using ClosedXML.Excel;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardImportCellAndParserTests
{
    [TestMethod]
    public void GetCellText_EmptyCell_ReturnsEmpty()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        Assert.AreEqual(string.Empty, InvoiceDashboardImportService.GetCellText(ws, 1, 1));
    }

    [TestMethod]
    public void GetCellText_TrimsWhitespace()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(2, 3).Value = "  hello  ";
        Assert.AreEqual("hello", InvoiceDashboardImportService.GetCellText(ws, 2, 3));
    }

    [TestMethod]
    public void GetCellText_OnlySpaces_ReturnsEmpty()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(1, 1).Value = "   ";
        Assert.AreEqual(string.Empty, InvoiceDashboardImportService.GetCellText(ws, 1, 1));
    }

    [TestMethod]
    public void IsBlankDataRow_AllNineEmpty_ReturnsTrue()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        Assert.IsTrue(InvoiceDashboardImportService.IsBlankDataRow(ws, 5));
    }

    [TestMethod]
    public void IsBlankDataRow_AllNineDashDisplay_ReturnsTrue()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        for (var c = 1; c <= DashboardInvoiceExcelLayout.ColumnCount; c++)
            ws.Cell(4, c).Value = DashboardInvoiceExcelLayout.EmptyCellDisplay;
        Assert.IsTrue(InvoiceDashboardImportService.IsBlankDataRow(ws, 4));
    }

    [TestMethod]
    public void IsBlankDataRow_MixEmptyAndDash_ReturnsTrue()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(1, 1).Value = DashboardInvoiceExcelLayout.EmptyCellDisplay;
        Assert.IsTrue(InvoiceDashboardImportService.IsBlankDataRow(ws, 1));
    }

    [TestMethod]
    public void IsBlankDataRow_OneNonEmpty_ReturnsFalse()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(1, 9).Value = "x";
        Assert.IsFalse(InvoiceDashboardImportService.IsBlankDataRow(ws, 1));
    }

    [TestMethod]
    public void TryParseDateOnly_EmptyAfterTrim_ReturnsFalse()
    {
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDateOnly("", out _));
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDateOnly("   ", out _));
    }

    [TestMethod]
    public void TryParseDateOnly_dd_MM_yyyy_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDateOnly("01/02/2026", out var d));
        Assert.AreEqual(new DateOnly(2026, 2, 1), d);
    }

    [TestMethod]
    public void TryParseDateOnly_d_M_yyyy_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDateOnly("1/2/2026", out var d));
        Assert.AreEqual(new DateOnly(2026, 2, 1), d);
    }

    [TestMethod]
    public void TryParseDateOnly_dd_M_yyyy_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDateOnly("02/1/2026", out var d));
        Assert.AreEqual(new DateOnly(2026, 1, 2), d);
    }

    [TestMethod]
    public void TryParseDateOnly_d_MM_yyyy_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDateOnly("2/01/2026", out var d));
        Assert.AreEqual(new DateOnly(2026, 1, 2), d);
    }

    [TestMethod]
    public void TryParseDateOnly_TrimsThenParses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDateOnly("  15/06/2026  ", out var d));
        Assert.AreEqual(new DateOnly(2026, 6, 15), d);
    }

    [TestMethod]
    public void TryParseDateOnly_Garbage_ReturnsFalse()
    {
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDateOnly("not-a-date", out _));
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDateOnly("2026-06-15", out _));
    }

    [TestMethod]
    public void TryParseDecimalFr_SpacesAndComma_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDecimalFr("1 234,56", out var v));
        Assert.AreEqual(1234.56m, v);
    }

    [TestMethod]
    public void TryParseDecimalFr_NbspAndSpaces_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDecimalFr("12\u00a0 0,5", out var v));
        Assert.AreEqual(120.5m, v);
    }

    [TestMethod]
    public void TryParseDecimalFr_InvariantDot_Parses()
    {
        Assert.IsTrue(InvoiceDashboardImportService.TryParseDecimalFr("1234.56", out var v));
        Assert.AreEqual(1234.56m, v);
    }

    [TestMethod]
    public void TryParseDecimalFr_EmptyOrLetters_ReturnsFalse()
    {
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDecimalFr("", out _));
        Assert.IsFalse(InvoiceDashboardImportService.TryParseDecimalFr("abc", out _));
    }
}
