using ClosedXML.Excel;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

/// <summary>
/// Tests for <see cref="DashboardInvoiceExcelHeaderFinder.FindHeaderRow"/> (header detection used by <see cref="InvoiceDashboardImportService"/>).
/// </summary>
[TestClass]
public sealed class InvoiceDashboardImportServiceTests
{
    /// <summary>Writes the nine canonical dashboard headers across columns 1–9 on <paramref name="row"/>.</summary>
    private static void WriteHeaderRow(IXLWorksheet ws, int row)
    {
        for (var c = 0; c < DashboardInvoiceExcelLayout.ColumnCount; c++)
            ws.Cell(row, c + 1).Value = DashboardInvoiceExcelLayout.ColumnHeaders[c];
    }

    // Export-style layout: title + stamp on rows 1–2, header row on row 3 → expect 3.
    [TestMethod]
    public void FindHeaderRow_HeaderOnRow3_Returns3()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        ws.Cell(1, 1).Value = "Title";
        ws.Cell(2, 1).Value = "Stamp";
        WriteHeaderRow(ws, 3);
        Assert.AreEqual(3, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Minimal file: all headers on the first sheet row → expect 1.
    [TestMethod]
    public void FindHeaderRow_HeaderOnRow1_Returns1()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 1);
        Assert.AreEqual(1, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Upper bound of the search window (rows 1–10): header on row 10 → expect 10.
    [TestMethod]
    public void FindHeaderRow_HeaderOnRow10_Returns10()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 10);
        Assert.AreEqual(10, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Row 2 matches only 8/9 headers (last cell wrong); row 5 is a full match → expect 5 (first complete row).
    [TestMethod]
    public void FindHeaderRow_SkipsPartialRow_ReturnsFirstFullMatch()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        for (var c = 0; c < DashboardInvoiceExcelLayout.ColumnCount - 1; c++)
            ws.Cell(2, c + 1).Value = DashboardInvoiceExcelLayout.ColumnHeaders[c];
        ws.Cell(2, 9).Value = "wrong";
        WriteHeaderRow(ws, 5);
        Assert.AreEqual(5, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Header below the scanned range (search stops at row 10) → expect null.
    [TestMethod]
    public void FindHeaderRow_HeaderOnlyOnRow11_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 11);
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Any single header cell text mismatch (typo) fails the whole row → expect null.
    [TestMethod]
    public void FindHeaderRow_OneTypoInHeader_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        ws.Cell(3, 5).Value = "Désignationx";
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Headers must match column index 1..9 to ColumnHeaders[0..8]; swapping two titles breaks order → null.
    [TestMethod]
    public void FindHeaderRow_WrongColumnOrder_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var row = 3;
        ws.Cell(row, 1).Value = DashboardInvoiceExcelLayout.ColumnHeaders[1];
        ws.Cell(row, 2).Value = DashboardInvoiceExcelLayout.ColumnHeaders[0];
        for (var c = 2; c < DashboardInvoiceExcelLayout.ColumnCount; c++)
            ws.Cell(row, c + 1).Value = DashboardInvoiceExcelLayout.ColumnHeaders[c];
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Trim() does not remove inner spaces; "Date  de facture" ≠ "Date de facture" → null.
    [TestMethod]
    public void FindHeaderRow_ExtraInnerSpace_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        ws.Cell(3, 1).Value = "Date  de facture";
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Ordinal match requires exact spelling including é in « préstation » → null if accent is wrong.
    [TestMethod]
    public void FindHeaderRow_WrongAccentOnLivraison_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        ws.Cell(3, 2).Value = "Date de livraison ou prestation";
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // StringComparison.Ordinal is case-sensitive for these French strings → null on ALL CAPS first header.
    [TestMethod]
    public void FindHeaderRow_CaseMismatch_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        ws.Cell(3, 1).Value = "DATE DE FACTURE";
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // GetCellText trims leading/trailing whitespace; header row still matches → expect 3.
    [TestMethod]
    public void FindHeaderRow_LeadingTrailingSpacesOnCells_StillMatches()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        ws.Cell(3, 1).Value = "  Date de facture  ";
        ws.Cell(3, 4).Value = "  Fournisseur  ";
        Assert.AreEqual(3, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // No header text in rows 1–10 → null.
    [TestMethod]
    public void FindHeaderRow_EmptySheet_ReturnsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        Assert.IsNull(DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }

    // Two rows both fully match: first row in scan order wins → expect 3, not 7.
    [TestMethod]
    public void FindHeaderRow_TwoFullHeaderRows_ReturnsFirst()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        WriteHeaderRow(ws, 3);
        WriteHeaderRow(ws, 7);
        Assert.AreEqual(3, DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws));
    }
}
