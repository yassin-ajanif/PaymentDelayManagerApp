using ClosedXML.Excel;

namespace PaymentDelayApp.Services;

/// <summary>
/// Locates the dashboard invoice Excel header row (same rules as import). Public for unit tests.
/// </summary>
public static class DashboardInvoiceExcelHeaderFinder
{
    public static int? FindHeaderRow(IXLWorksheet ws)
    {
        for (var r = DashboardInvoiceExcelLayout.HeaderSearchFirstRow; r <= DashboardInvoiceExcelLayout.HeaderSearchLastRow; r++)
        {
            var ok = true;
            for (var c = 0; c < DashboardInvoiceExcelLayout.ColumnCount; c++)
            {
                var cellText = GetCellText(ws, r, c + 1);
                if (!string.Equals(cellText, DashboardInvoiceExcelLayout.ColumnHeaders[c], StringComparison.Ordinal))
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
                return r;
        }

        return null;
    }

    private static string GetCellText(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty())
            return string.Empty;
        return cell.GetString().Trim();
    }
}
