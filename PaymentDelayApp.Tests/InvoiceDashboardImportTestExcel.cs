using System.IO;
using ClosedXML.Excel;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

/// <summary>Shared ClosedXML helpers for dashboard invoice import tests.</summary>
internal static class InvoiceDashboardImportTestExcel
{
    public static void WriteHeaderRow(IXLWorksheet ws, int row)
    {
        for (var c = 0; c < DashboardInvoiceExcelLayout.ColumnCount; c++)
            ws.Cell(row, c + 1).Value = DashboardInvoiceExcelLayout.ColumnHeaders[c];
    }

    public static void WriteDataCells(IXLWorksheet ws, int row, ReadOnlySpan<string> nineCells)
    {
        for (var i = 0; i < DashboardInvoiceExcelLayout.ColumnCount; i++)
            ws.Cell(row, i + 1).Value = i < nineCells.Length ? nineCells[i] : string.Empty;
    }

    public static MemoryStream WorkbookToRewindableStream(XLWorkbook wb)
    {
        using var buffer = new MemoryStream();
        wb.SaveAs(buffer);
        return new MemoryStream(buffer.ToArray());
    }
}
