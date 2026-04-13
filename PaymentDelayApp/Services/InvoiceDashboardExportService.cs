using ClosedXML.Excel;
using PaymentDelayApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaymentDelayApp.Services;

public sealed class InvoiceDashboardExportService : IInvoiceDashboardExportService
{
    private static IReadOnlyList<string> Headers => DashboardInvoiceExcelLayout.ColumnHeaders;

    static InvoiceDashboardExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task WritePdfAsync(
        IReadOnlyList<InvoiceDashboardRow> rows,
        Stream destination,
        string reportTitle,
        string exportTimestampLine,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(12);
                    page.DefaultTextStyle(x => x.FontSize(7));
                    page.Content().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Text(reportTitle).FontSize(12).SemiBold();
                        column.Item().Text(exportTimestampLine).FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(0.7f);
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(1.4f);
                                c.RelativeColumn(0.7f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(0.7f);
                                c.RelativeColumn(1f);
                            });

                            static IContainer CellStyle(IContainer cell) =>
                                cell.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).AlignMiddle();

                            table.Header(header =>
                            {
                                foreach (var title in DashboardInvoiceExcelLayout.ColumnHeaders)
                                    header.Cell().Element(CellStyle).Text(title).SemiBold();
                            });

                            foreach (var row in rows)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                table.Cell().Element(CellStyle).Text(row.DateFactureDisplay);
                                table.Cell().Element(CellStyle).Text(row.DateLivraisonDisplay);
                                table.Cell().Element(CellStyle).Text(row.InvoiceNumber);
                                table.Cell().Element(CellStyle).Text(row.SupplierName);
                                table.Cell().Element(CellStyle).Text(row.Designation ?? string.Empty);
                                table.Cell().Element(CellStyle).Text(row.TtcDisplay);
                                table.Cell().Element(CellStyle).Text(row.EcheanceRespecteeDisplay);
                                table.Cell().Element(CellStyle).Text(row.ResteDisplay);
                                table.Cell().Element(CellStyle).Text(row.EcheanceFactureDisplay);
                            }
                        });
                    });
                });
            })
            .GeneratePdf(destination);

        return Task.CompletedTask;
    }

    public Task WriteExcelAsync(
        IReadOnlyList<InvoiceDashboardRow> rows,
        Stream destination,
        string reportTitle,
        string exportTimestampLine,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var sheetName = SanitizeSheetName(reportTitle);
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Range(1, 1, 1, Headers.Count).Merge();
        ws.Cell(1, 1).Value = reportTitle;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Range(2, 1, 2, Headers.Count).Merge();
        ws.Cell(2, 1).Value = exportTimestampLine;
        ws.Cell(2, 1).Style.Font.FontSize = 10;

        var headerRow = DashboardInvoiceExcelLayout.HeaderRowNumber;
        for (var c = 0; c < Headers.Count; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = Headers[c];
            cell.Style.Font.Bold = true;
        }

        var r = DashboardInvoiceExcelLayout.FirstDataRowNumber;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ws.Cell(r, 1).Value = row.DateFactureDisplay;
            ws.Cell(r, 2).Value = row.DateLivraisonDisplay;
            ws.Cell(r, 3).Value = row.InvoiceNumber;
            ws.Cell(r, 4).Value = row.SupplierName;
            ws.Cell(r, 5).Value = row.Designation ?? string.Empty;
            ws.Cell(r, 6).Value = row.TtcDisplay;
            ws.Cell(r, 7).Value = row.EcheanceRespecteeDisplay;
            ws.Cell(r, 8).Value = row.ResteDisplay;
            ws.Cell(r, 9).Value = row.EcheanceFactureDisplay;
            r++;
        }

        ws.Columns().AdjustToContents();
        workbook.Properties.Title = reportTitle;
        workbook.SaveAs(destination);

        return Task.CompletedTask;
    }

    private static string SanitizeSheetName(string title)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var s = string.Join("", title.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (s.Length == 0)
            s = "Factures";
        return s.Length <= 31 ? s : s[..31];
    }
}
