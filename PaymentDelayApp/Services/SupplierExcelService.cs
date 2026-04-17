using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.DataAccessLayer;

namespace PaymentDelayApp.Services;

public sealed class SupplierExcelService : ISupplierExcelService
{
    private const int MaxNameLen = 500;
    private const int MaxIceLen = 100;
    private const int MaxFiscalLen = 100;
    private const int MaxAddressLen = 1000;
    private const int MaxActiviteLen = 500;
    private const int DefaultSeuil = 15;
    private const int MinSeuil = 1;
    private const int MaxSeuil = 120;

    private static readonly CultureInfo FrCulture = CultureInfo.GetCultureInfo("fr-FR");

    private static IReadOnlyList<string> Headers => SupplierExcelLayout.ColumnHeaders;

    private readonly ISupplierService _supplierService;
    private readonly ISupplierAccess _supplierAccess;
    private readonly PaymentDelayDbContext _db;

    public SupplierExcelService(
        ISupplierService supplierService,
        ISupplierAccess supplierAccess,
        PaymentDelayDbContext db)
    {
        _supplierService = supplierService;
        _supplierAccess = supplierAccess;
        _db = db;
    }

    public Task WriteExcelAsync(
        IReadOnlyList<Supplier> suppliers,
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

        var headerRow = SupplierExcelLayout.HeaderRowNumber;
        for (var c = 0; c < Headers.Count; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = Headers[c];
            cell.Style.Font.Bold = true;
        }

        var r = SupplierExcelLayout.FirstDataRowNumber;
        foreach (var s in suppliers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ws.Cell(r, 1).Value = s.Name;
            ws.Cell(r, 2).Value = s.Ice ?? string.Empty;
            ws.Cell(r, 3).Value = s.FiscalId ?? string.Empty;
            ws.Cell(r, 4).Value = s.Address ?? string.Empty;
            ws.Cell(r, 5).Value = s.Activite ?? string.Empty;
            ws.Cell(r, 6).Value = s.AlertSeuilJours;
            r++;
        }

        ws.Columns().AdjustToContents();
        workbook.Properties.Title = reportTitle;
        workbook.SaveAs(destination);

        return Task.CompletedTask;
    }

    public async Task<SupplierExcelImportResult> ImportFromExcelAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return SupplierExcelImportResult.HeaderFailure(["Feuille introuvable."]);

        var headerRow = FindHeaderRow(ws);
        if (headerRow is null)
        {
            return SupplierExcelImportResult.HeaderFailure(
                ["Ligne d'en-tête introuvable : les six colonnes attendues n'ont pas été trouvées telles quelles dans les 10 premières lignes."]);
        }

        var suppliers = await _supplierService.GetSuppliersAsync(cancellationToken).ConfigureAwait(false);
        var supplierByName = BuildSupplierLookup(suppliers, out var ambiguousNames);

        var rowErrors = new List<(int ExcelRow, string Message)>();
        var parsed = new List<ParsedSupplierRow>();

        var dataStartRow = headerRow.Value + 1;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow.Value;
        for (var excelRow = dataStartRow; excelRow <= lastRow; excelRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBlankDataRow(ws, excelRow))
                continue;

            var err = TryParseRow(ws, excelRow, supplierByName, ambiguousNames, out var supplier);
            if (err is not null)
                rowErrors.Add((excelRow, err));
            else if (supplier is not null)
                parsed.Add(new ParsedSupplierRow(excelRow, supplier));
        }

        AddInFileDuplicateNomErrors(parsed, rowErrors);
        AddInFileDuplicateIceErrors(parsed, rowErrors);
        AddInFileDuplicateFiscalIdErrors(parsed, rowErrors);

        await AddDbValidationErrorsAsync(parsed, rowErrors, cancellationToken).ConfigureAwait(false);

        if (rowErrors.Count > 0)
            return SupplierExcelImportResult.RowFailures(rowErrors);

        if (parsed.Count == 0)
            return SupplierExcelImportResult.Ok(0, 0);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var inserted = 0;
        var updated = 0;
        try
        {
            foreach (var item in parsed.OrderBy(p => p.ExcelRow))
            {
                var wasInsert = item.Supplier.Id == 0;
                await _supplierService.SaveSupplierAsync(item.Supplier, cancellationToken).ConfigureAwait(false);
                if (wasInsert)
                    inserted++;
                else
                    updated++;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return SupplierExcelImportResult.Ok(inserted, updated);
    }

    private static int? FindHeaderRow(IXLWorksheet ws)
    {
        for (var r = SupplierExcelLayout.HeaderSearchFirstRow; r <= SupplierExcelLayout.HeaderSearchLastRow; r++)
        {
            var ok = true;
            for (var c = 0; c < SupplierExcelLayout.ColumnCount; c++)
            {
                var cellText = GetCellText(ws, r, c + 1);
                if (!string.Equals(cellText, SupplierExcelLayout.ColumnHeaders[c], StringComparison.Ordinal))
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

    private static bool IsBlankDataRow(IXLWorksheet ws, int row)
    {
        for (var c = 1; c <= SupplierExcelLayout.ColumnCount; c++)
        {
            if (c == 6)
            {
                if (!IsSeuilCellEmpty(ws, row, c))
                    return false;
                continue;
            }

            if (GetCellText(ws, row, c).Length > 0)
                return false;
        }

        return true;
    }

    private static bool IsSeuilCellEmpty(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty())
            return true;
        if (cell.DataType == XLDataType.Number)
            return false;
        return string.IsNullOrWhiteSpace(cell.GetString());
    }

    private static bool TryParseSeuil(IXLWorksheet ws, int row, int col, out int seuil, out string? error)
    {
        seuil = DefaultSeuil;
        error = null;
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty())
            return true;

        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            var rounded = (int)Math.Round(d, MidpointRounding.AwayFromZero);
            if (Math.Abs(d - rounded) > 1e-6)
            {
                error = "Seuil alerte (j) doit être un entier entre 1 et 120, ou vide (défaut 15).";
                return false;
            }

            seuil = rounded;
        }
        else
        {
            var t = cell.GetString().Trim();
            if (t.Length == 0)
                return true;
            if (!int.TryParse(t, NumberStyles.Integer, FrCulture, out seuil)
                && !int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out seuil))
            {
                error = "Seuil alerte (j) invalide.";
                return false;
            }
        }

        if (seuil is < MinSeuil or > MaxSeuil)
        {
            error = $"Seuil alerte (j) doit être entre {MinSeuil} et {MaxSeuil}, ou vide (défaut {DefaultSeuil}).";
            return false;
        }

        return true;
    }

    private static Dictionary<string, Supplier> BuildSupplierLookup(
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

    private static string? TryParseRow(
        IXLWorksheet ws,
        int excelRow,
        IReadOnlyDictionary<string, Supplier> supplierByName,
        HashSet<string> ambiguousNames,
        out Supplier? supplier)
    {
        supplier = null;

        var nom = GetCellText(ws, excelRow, 1);
        var iceRaw = GetCellText(ws, excelRow, 2);
        var fiscalRaw = GetCellText(ws, excelRow, 3);
        var addressRaw = GetCellText(ws, excelRow, 4);
        var activiteRaw = GetCellText(ws, excelRow, 5);

        if (nom.Length == 0)
            return "Nom obligatoire.";
        if (nom.Length > MaxNameLen)
            return $"Nom trop long (max {MaxNameLen} caractères).";

        var ice = string.IsNullOrWhiteSpace(iceRaw) ? null : iceRaw.Trim();
        if (ice is { Length: > 0 } && ice.Length > MaxIceLen)
            return $"ICE trop long (max {MaxIceLen} caractères).";

        var fiscalId = string.IsNullOrWhiteSpace(fiscalRaw) ? null : fiscalRaw.Trim();
        if (fiscalId is { Length: > 0 } && fiscalId.Length > MaxFiscalLen)
            return $"IF trop long (max {MaxFiscalLen} caractères).";

        var address = string.IsNullOrWhiteSpace(addressRaw) ? null : addressRaw.Trim();
        if (address is { Length: > 0 } && address.Length > MaxAddressLen)
            return $"Adresse trop longue (max {MaxAddressLen} caractères).";

        var activite = string.IsNullOrWhiteSpace(activiteRaw) ? null : activiteRaw.Trim();
        if (activite is { Length: > 0 } && activite.Length > MaxActiviteLen)
            return $"Activite trop longue (max {MaxActiviteLen} caractères).";

        if (!TryParseSeuil(ws, excelRow, 6, out var seuil, out var seuilErr))
            return seuilErr;

        var nameKey = SupplierNameNormalizer.NormalizeKey(nom);
        if (ambiguousNames.Contains(nameKey))
            return $"Nom « {nom} » ambigu (plusieurs entrées en base).";

        if (supplierByName.TryGetValue(nameKey, out _))
            return "Ce fournisseur existe déjà.";

        supplier = new Supplier
        {
            Id = 0,
            Name = nom.Trim(),
            Ice = ice,
            FiscalId = fiscalId,
            Address = address,
            Activite = activite,
            AlertSeuilJours = seuil,
        };
        return null;
    }

    private static void AddInFileDuplicateNomErrors(
        IReadOnlyList<ParsedSupplierRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors)
    {
        var firstRowByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.OrderBy(x => x.ExcelRow))
        {
            var key = SupplierNameNormalizer.NormalizeKey(p.Supplier.Name);
            if (firstRowByKey.TryGetValue(key, out var firstRow))
                rowErrors.Add((p.ExcelRow, $"Même nom qu'à la ligne {firstRow}."));
            else
                firstRowByKey[key] = p.ExcelRow;
        }
    }

    private static void AddInFileDuplicateIceErrors(
        IReadOnlyList<ParsedSupplierRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors)
    {
        var firstRowByIce = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.OrderBy(x => x.ExcelRow))
        {
            var ice = p.Supplier.Ice;
            if (string.IsNullOrEmpty(ice))
                continue;
            if (firstRowByIce.TryGetValue(ice, out var firstRow))
                rowErrors.Add((p.ExcelRow, $"Même ICE qu'à la ligne {firstRow}."));
            else
                firstRowByIce[ice] = p.ExcelRow;
        }
    }

    private static void AddInFileDuplicateFiscalIdErrors(
        IReadOnlyList<ParsedSupplierRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors)
    {
        var firstRowByFiscal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.OrderBy(x => x.ExcelRow))
        {
            var fid = p.Supplier.FiscalId;
            if (string.IsNullOrEmpty(fid))
                continue;
            if (firstRowByFiscal.TryGetValue(fid, out var firstRow))
                rowErrors.Add((p.ExcelRow, $"Même IF qu'à la ligne {firstRow}."));
            else
                firstRowByFiscal[fid] = p.ExcelRow;
        }
    }

    private async Task AddDbValidationErrorsAsync(
        IReadOnlyList<ParsedSupplierRow> parsed,
        List<(int ExcelRow, string Message)> rowErrors,
        CancellationToken cancellationToken)
    {
        foreach (var p in parsed.OrderBy(x => x.ExcelRow))
        {
            var excludeId = p.Supplier.Id == 0 ? (int?)null : p.Supplier.Id;

            if (await _supplierAccess.NameExistsAsync(p.Supplier.Name, excludeId, cancellationToken).ConfigureAwait(false))
                rowErrors.Add((p.ExcelRow, "Un autre fournisseur porte déjà ce nom."));

            if (!string.IsNullOrWhiteSpace(p.Supplier.Ice)
                && await _supplierAccess.IceExistsAsync(p.Supplier.Ice, excludeId, cancellationToken).ConfigureAwait(false))
                rowErrors.Add((p.ExcelRow, "Un autre fournisseur utilise déjà cet ICE."));

            if (!string.IsNullOrWhiteSpace(p.Supplier.FiscalId)
                && await _supplierAccess.FiscalIdExistsAsync(p.Supplier.FiscalId, excludeId, cancellationToken)
                    .ConfigureAwait(false))
                rowErrors.Add((p.ExcelRow, "Un autre fournisseur utilise déjà cet IF."));
        }
    }

    private static string SanitizeSheetName(string title)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var s = string.Join("", title.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (s.Length == 0)
            s = "Fournisseurs";
        return s.Length <= 31 ? s : s[..31];
    }

    private sealed record ParsedSupplierRow(int ExcelRow, Supplier Supplier);
}
