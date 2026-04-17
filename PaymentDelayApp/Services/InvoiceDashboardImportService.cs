using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;

namespace PaymentDelayApp.Services;

public sealed class InvoiceDashboardImportService : IInvoiceDashboardImportService
{
    private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy"];
    private static readonly Regex ResteJoursRegex = new(@"^(-?\d+)\s*j\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IInvoiceAccess _invoiceAccess;
    private readonly PaymentDelayDbContext _db;

    public InvoiceDashboardImportService(
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IInvoiceAccess invoiceAccess,
        PaymentDelayDbContext db)
    {
        _invoiceService = invoiceService;
        _supplierService = supplierService;
        _invoiceAccess = invoiceAccess;
        _db = db;
    }

    public async Task<InvoiceDashboardImportResult> ImportFromExcelAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return InvoiceDashboardImportResult.HeaderFailure(["Feuille introuvable."]);

        var headerRow = DashboardInvoiceExcelHeaderFinder.FindHeaderRow(ws);
        if (headerRow is null)
        {
            return InvoiceDashboardImportResult.HeaderFailure(
                ["Ligne d'en-tête introuvable : les neuf colonnes attendues n'ont pas été trouvées telles quelles dans les 10 premières lignes."]);
        }

        var suppliers = await _supplierService.GetSuppliersAsync(cancellationToken);
        var supplierByName = BuildSupplierLookup(suppliers, out var ambiguousNames);

        var rowErrors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<InvoiceDashboardImportParsedRow>();

        var dataStartRow = headerRow.Value + 1;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow.Value;
        for (var excelRow = dataStartRow; excelRow <= lastRow; excelRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBlankDataRow(ws, excelRow))
                continue;

            var err = TryParseRow(
                ws,
                excelRow,
                supplierByName,
                ambiguousNames,
                out var invoice);
            if (err is not null)
                rowErrors.Add((excelRow, err));
            else if (invoice is not null)
                parsed.Add(new InvoiceDashboardImportParsedRow(excelRow, invoice));
        }

        AddInFileDuplicateErrors(parsed, rowErrors);
        await AddDatabaseDuplicateErrorsAsync(parsed, rowErrors, cancellationToken).ConfigureAwait(false);

        if (rowErrors.Count > 0)
            return InvoiceDashboardImportResult.RowFailures(rowErrors);

        if (parsed.Count == 0)
            return InvoiceDashboardImportResult.Ok(0);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var item in parsed.OrderBy(p => p.ExcelRow))
                await _invoiceService.SaveInvoiceAsync(item.Invoice, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return InvoiceDashboardImportResult.Ok(parsed.Count);
    }

    internal static string GetCellText(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty())
            return string.Empty;
        return cell.GetString().Trim();
    }

    internal static bool IsBlankDataRow(IXLWorksheet ws, int row)
    {
        for (var c = 1; c <= DashboardInvoiceExcelLayout.ColumnCount; c++)
        {
            var t = GetCellText(ws, row, c);
            if (t.Length > 0 && t != DashboardInvoiceExcelLayout.EmptyCellDisplay)
                return false;
        }

        return true;
    }

    internal static Dictionary<string, Supplier> BuildSupplierLookup(
        IReadOnlyList<Supplier> suppliers,
        out HashSet<string> ambiguousNames)
    {
        ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, Supplier>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in suppliers)
        {
            var key = SupplierNameNormalizer.NormalizeKey(s.Name);
            if (key.Length == 0)
                continue;
            if (map.ContainsKey(key))
                ambiguousNames.Add(key);
            else
                map[key] = s;
        }

        foreach (var a in ambiguousNames)
            map.Remove(a);
        return map;
    }

    internal static string? TryParseRow(
        IXLWorksheet ws,
        int excelRow,
        IReadOnlyDictionary<string, Supplier> supplierByName,
        HashSet<string> ambiguousNames,
        out Invoice? invoice)
    {
        invoice = null;
        var calendarYear = DateTime.Today.Year;

        var c1 = GetCellText(ws, excelRow, 1);
        var c2 = GetCellText(ws, excelRow, 2);
        var c3 = GetCellText(ws, excelRow, 3);
        var c4 = GetCellText(ws, excelRow, 4);
        var c5 = GetCellText(ws, excelRow, 5);
        var c6 = GetCellText(ws, excelRow, 6);
        var c7 = GetCellText(ws, excelRow, 7);
        var c8 = GetCellText(ws, excelRow, 8);
        var c9 = GetCellText(ws, excelRow, 9);

        if (!TryParseDateOnly(c1, out var invoiceDate))
            return "Date de facture invalide ou vide.";
        if (invoiceDate.Year != calendarYear)
            return $"La date de facture doit être dans l'année {calendarYear}.";

        DateOnly? delivery = null;
        if (c2.Length > 0 && c2 != DashboardInvoiceExcelLayout.EmptyCellDisplay)
        {
            if (!TryParseDateOnly(c2, out var del))
                return "Date de livraison ou prestation invalide.";
            if (del.Year != calendarYear)
                return $"La date de livraison doit être dans l'année {calendarYear}.";
            delivery = del;
        }

        if (c3.Length == 0)
            return "N° de Facture obligatoire.";
        if (c4.Length == 0)
            return "Fournisseur obligatoire.";
        var supplierKey = SupplierNameNormalizer.NormalizeKey(c4);
        if (ambiguousNames.Contains(supplierKey))
            return $"Fournisseur « {c4} » ambigu (plusieurs entrées).";
        if (!supplierByName.TryGetValue(supplierKey, out var supplier))
            return $"Fournisseur inconnu : « {c4} ».";

        if (c6.Length == 0)
            return "TTC obligatoire.";
        if (!TryParseDecimalFr(c6, out var ttc))
            return "TTC invalide.";

        if (c7.Length == 0 || c7 == DashboardInvoiceExcelLayout.EmptyCellDisplay)
            return "Date d'échéance respectée obligatoire.";
        if (!TryParseDateOnly(c7, out var dateLimite7))
            return "Date d'échéance respectée invalide.";

        if (c9.Length == 0 || c9 == DashboardInvoiceExcelLayout.EmptyCellDisplay)
            return "Date d'échéance/facture obligatoire.";
        if (!TryParseDateOnly(c9, out var dateLimite9))
            return "Date d'échéance/facture invalide.";

        if (dateLimite7 != dateLimite9)
            return "Les dates d'échéance (colonnes 7 et 9) ne correspondent pas.";

        var echeanceJours = dateLimite7.DayNumber - invoiceDate.DayNumber;
        if (echeanceJours is < 0 or > 120)
            return "Délai d'échéance dérivé hors plage (0–120 jours).";

        var expectedLimit = EcheanceCalculator.DateEcheanceNormale(invoiceDate, echeanceJours);
        if (expectedLimit != dateLimite7)
            return "Incohérence entre date de facture et date limite.";

        if (c8.Length == 0 || c8 == DashboardInvoiceExcelLayout.EmptyCellDisplay)
            return "Reste des jours obligatoire.";
        if (!ResteJoursRegex.IsMatch(c8.Trim()))
            return "Reste des jours invalide (format attendu : « 5 j »).";
        // Do not require match with today's computed reste: export is a snapshot; days change after export.

        invoice = new Invoice
        {
            Id = 0,
            SupplierId = supplier.Id,
            Supplier = null,
            InvoiceDate = invoiceDate,
            DeliveryOrServiceDate = delivery,
            InvoiceNumber = c3.Trim(),
            Designation = string.IsNullOrWhiteSpace(c5) ? null : c5.Trim(),
            TtcAmount = ttc,
            EcheanceFactureJours = echeanceJours,
            IsSettled = false,
            IsPaymentAlert = false,
            PaidAt = null,
        };

        return null;
    }

    internal static void AddInFileDuplicateErrors(
        IReadOnlyList<InvoiceDashboardImportParsedRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors)
    {
        var firstRowByKey = new Dictionary<(int SupplierId, string Number), int>();
        foreach (var p in parsed.OrderBy(x => x.ExcelRow))
        {
            var num = p.Invoice.InvoiceNumber.Trim();
            var key = (p.Invoice.SupplierId, num);
            if (firstRowByKey.TryGetValue(key, out var firstRow))
                rowErrors.Add((p.ExcelRow, $"Même fournisseur et numéro de facture qu'à la ligne {firstRow}."));
            else
                firstRowByKey[key] = p.ExcelRow;
        }
    }

    internal async Task AddDatabaseDuplicateErrorsAsync(
        IReadOnlyList<InvoiceDashboardImportParsedRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors,
        CancellationToken cancellationToken)
    {
        foreach (var p in parsed)
        {
            var exists = await _invoiceAccess.ExistsWithSupplierAndNumberAsync(
                p.Invoice.SupplierId,
                p.Invoice.InvoiceNumber.Trim(),
                excludeInvoiceId: null,
                cancellationToken).ConfigureAwait(false);
            if (exists)
                rowErrors.Add((p.ExcelRow, "Une facture avec ce numéro existe déjà pour ce fournisseur."));
        }
    }

    internal static bool TryParseDateOnly(string text, out DateOnly date)
    {
        text = text.Trim();
        return DateOnly.TryParseExact(text, DateFormats, FrCulture, DateTimeStyles.None, out date);
    }

    internal static bool TryParseDecimalFr(string text, out decimal value)
    {
        text = text.Trim().Replace('\u00a0', ' ').Replace(" ", "", StringComparison.Ordinal);
        return decimal.TryParse(text, NumberStyles.Number, FrCulture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

}
