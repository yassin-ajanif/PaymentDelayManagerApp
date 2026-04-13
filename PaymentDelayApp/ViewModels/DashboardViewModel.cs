using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private static readonly FilePickerFileType[] PdfSaveTypes =
    [
        new("PDF") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] },
    ];

    private static readonly FilePickerFileType[] ExcelSaveTypes =
    [
        new("Excel") { Patterns = ["*.xlsx"], MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] },
    ];

    private readonly IInvoiceService _invoiceService;
    private readonly IDialogService _dialogs;
    private readonly IInvoiceDashboardExportService _export;
    private readonly IInvoiceDashboardImportService _import;
    private List<InvoiceDashboardRow> _allRows = [];

    [ObservableProperty]
    private ObservableCollection<InvoiceDashboardRow> _rows = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Filtre plage : date de facture ≥ (inclus). Null = pas de borne basse.</summary>
    [ObservableProperty]
    private DateTimeOffset? _invoiceDateFilterFrom;

    /// <summary>Filtre plage : date de facture ≤ (inclus). Null = pas de borne haute.</summary>
    [ObservableProperty]
    private DateTimeOffset? _invoiceDateFilterTo;

    [ObservableProperty]
    private bool _showAlertInvoicesOnly;

    [ObservableProperty]
    private bool _showSettledInvoicesOnly;

    [ObservableProperty]
    private bool _showUnsettledInvoicesOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlertFilterChipCaption))]
    private int _alertInvoiceCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SettledFilterChipCaption))]
    private int _settledInvoiceCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnsettledFilterChipCaption))]
    private int _unsettledInvoiceCount;

    [ObservableProperty]
    private InvoiceDashboardRow? _selectedRow;

    /// <summary>Label for the alert-only filter chip (count from full list, not filtered grid).</summary>
    public string AlertFilterChipCaption =>
        AlertInvoiceCount == 0
            ? "Factures en alerte"
            : $"Factures en alerte ({AlertInvoiceCount})";

    public string SettledFilterChipCaption =>
        SettledInvoiceCount == 0
            ? "Factures réglées"
            : $"Factures réglées ({SettledInvoiceCount})";

    public string UnsettledFilterChipCaption =>
        UnsettledInvoiceCount == 0
            ? "Factures non réglées"
            : $"Factures non réglées ({UnsettledInvoiceCount})";

    /// <summary>Bornes DatePicker pour le filtre date de facture (année en cours uniquement).</summary>
    public DateTimeOffset InvoiceDateFilterPickerMin { get; } =
        new(new DateTime(DateTime.Today.Year, 1, 1, 0, 0, 0, DateTimeKind.Local));

    public DateTimeOffset InvoiceDateFilterPickerMax { get; } =
        new(new DateTime(DateTime.Today.Year, 12, 31, 0, 0, 0, DateTimeKind.Local));

    public DashboardViewModel()
    {
        _invoiceService = null!;
        _dialogs = null!;
        _export = null!;
        _import = null!;
    }

    public DashboardViewModel(
        IInvoiceService invoiceService,
        IDialogService dialogs,
        IInvoiceDashboardExportService export,
        IInvoiceDashboardImportService import)
    {
        _invoiceService = invoiceService;
        _dialogs = dialogs;
        _export = export;
        _import = import;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_invoiceService is null)
            return;

        await _invoiceService.RefreshPaymentAlertFlagsAsync(cancellationToken);
        var list = await _invoiceService.GetInvoicesAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        _allRows = list.Select(i => InvoiceDashboardRow.FromInvoice(i, today)).ToList();
        AlertInvoiceCount = _allRows.Count(r => r.IsResteJoursAlert);
        SettledInvoiceCount = _allRows.Count(r => r.IsSettled);
        UnsettledInvoiceCount = _allRows.Count(r => !r.IsSettled);
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnInvoiceDateFilterFromChanged(DateTimeOffset? value) => ApplyFilter();

    partial void OnInvoiceDateFilterToChanged(DateTimeOffset? value) => ApplyFilter();

    partial void OnShowAlertInvoicesOnlyChanged(bool value)
    {
        if (value)
        {
            ShowSettledInvoicesOnly = false;
            ShowUnsettledInvoicesOnly = false;
        }
        ApplyFilter();
    }

    partial void OnShowSettledInvoicesOnlyChanged(bool value)
    {
        if (value)
        {
            ShowAlertInvoicesOnly = false;
            ShowUnsettledInvoicesOnly = false;
        }
        ApplyFilter();
    }

    partial void OnShowUnsettledInvoicesOnlyChanged(bool value)
    {
        if (value)
        {
            ShowAlertInvoicesOnly = false;
            ShowSettledInvoicesOnly = false;
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<InvoiceDashboardRow> src = _allRows;
        if (ShowAlertInvoicesOnly)
            src = src.Where(r => r.IsResteJoursAlert);
        else if (ShowSettledInvoicesOnly)
            src = src.Where(r => r.IsSettled);
        else if (ShowUnsettledInvoicesOnly)
            src = src.Where(r => !r.IsSettled);

        var q = SearchText.Trim();
        if (q.Length > 0)
        {
            src = src.Where(r =>
                r.InvoiceNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.SupplierName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var from = ToDateOnly(InvoiceDateFilterFrom);
        var to = ToDateOnly(InvoiceDateFilterTo);
        if (from is not null && to is not null && from > to)
            (from, to) = (to, from);

        if (from is not null)
            src = src.Where(r => r.InvoiceDate >= from.Value);
        if (to is not null)
            src = src.Where(r => r.InvoiceDate <= to.Value);

        Rows = new ObservableCollection<InvoiceDashboardRow>(src);
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? dto) =>
        dto is null ? null : DateOnly.FromDateTime(dto.Value.LocalDateTime.Date);

    /// <summary>Document title for exports from the active list filter chip (mutually exclusive).</summary>
    private string GetExportScopeTitle()
    {
        if (ShowAlertInvoicesOnly)
            return "Factures en alerte";
        if (ShowUnsettledInvoicesOnly)
            return "Factures non réglées";
        if (ShowSettledInvoicesOnly)
            return "Factures réglées";
        return "Toutes les factures";
    }

    private static string GetExportTimestampLine()
    {
        var n = DateTime.Now;
        return $"Exporté le {n:dd/MM/yyyy} à {n:HH:mm}";
    }

    private string BuildSuggestedExportFileName(string extension)
    {
        var slug = ShowAlertInvoicesOnly
            ? "en_alerte"
            : ShowUnsettledInvoicesOnly
                ? "non_reglees"
                : ShowSettledInvoicesOnly
                    ? "reglees"
                    : "toutes";
        return $"Factures_{slug}_{DateTime.Now:yyyyMMdd_HHmm}.{extension}";
    }

    [RelayCommand]
    private void ClearInvoiceDateFilters()
    {
        if (InvoiceDateFilterFrom is null && InvoiceDateFilterTo is null)
            return;
        InvoiceDateFilterFrom = null;
        InvoiceDateFilterTo = null;
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null || _export is null)
            return;
        if (Rows.Count == 0)
        {
            await _dialogs.ShowMessageAsync("Export", "Aucune ligne à exporter.");
            return;
        }

        var suggested = BuildSuggestedExportFileName("pdf");
        var file = await _dialogs.PickSaveExportFileAsync(suggested, PdfSaveTypes, null, cancellationToken);
        if (file is null)
            return;

        var snapshot = Rows.ToList();
        var title = GetExportScopeTitle();
        var stamp = GetExportTimestampLine();
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await _export.WritePdfAsync(snapshot, stream, title, stamp, cancellationToken);
            await _dialogs.ShowMessageAsync("Export", "Fichier PDF enregistré.");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Export", $"Erreur lors de l'export : {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null || _export is null)
            return;
        if (Rows.Count == 0)
        {
            await _dialogs.ShowMessageAsync("Export", "Aucune ligne à exporter.");
            return;
        }

        var suggested = BuildSuggestedExportFileName("xlsx");
        var file = await _dialogs.PickSaveExportFileAsync(suggested, ExcelSaveTypes, null, cancellationToken);
        if (file is null)
            return;

        var snapshot = Rows.ToList();
        var title = GetExportScopeTitle();
        var stamp = GetExportTimestampLine();
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await _export.WriteExcelAsync(snapshot, stream, title, stamp, cancellationToken);
            await _dialogs.ShowMessageAsync("Export", "Fichier Excel enregistré.");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Export", $"Erreur lors de l'export : {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportExcelAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null || _import is null)
            return;
        var file = await _dialogs.PickOpenImportExcelFileAsync(null, cancellationToken);
        if (file is null)
            return;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var result = await _import.ImportFromExcelAsync(stream, cancellationToken);
            await _dialogs.ShowMessageAsync("Import Excel", FormatImportResultMessage(result));
            if (result.Success && _invoiceService is not null)
                await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Import Excel", $"Erreur : {ex.Message}");
        }
    }

    private static string FormatImportResultMessage(InvoiceDashboardImportResult r)
    {
        if (r.MissingHeaders.Count > 0)
            return string.Join(Environment.NewLine, r.MissingHeaders);
        if (!r.Success)
        {
            var lines = r.RowErrors.Take(50).Select(e => $"Ligne {e.ExcelRow} : {e.Message}");
            var body = string.Join(Environment.NewLine, lines);
            if (r.RowErrors.Count > 50)
                body += $"{Environment.NewLine}… et {r.RowErrors.Count - 50} autre(s) erreur(s).";
            return $"Import annulé — aucune facture enregistrée.{Environment.NewLine}{Environment.NewLine}{r.RowErrors.Count} erreur(s) :{Environment.NewLine}{body}";
        }

        return r.ImportedCount == 0
            ? "Aucune ligne de données à importer (fichier vide ou uniquement des lignes vides)."
            : $"{r.ImportedCount} facture(s) importée(s).";
    }

    [RelayCommand]
    private async Task OpenWatcherSettingsAsync()
    {
        if (_dialogs is null)
            return;
        await _dialogs.ShowSettingsAsync();
    }

    [RelayCommand]
    private async Task OpenBackupSettingsAsync()
    {
        if (_dialogs is null)
            return;
        await _dialogs.ShowBackupSettingsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_invoiceService is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task AddSupplierAsync()
    {
        if (_dialogs is null)
            return;
        var ok = await _dialogs.ShowSupplierFormAsync(null);
        if (ok == true && _invoiceService is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task NewInvoiceAsync()
    {
        if (_dialogs is null)
            return;
        var ok = await _dialogs.ShowInvoiceEditAsync(null);
        if (ok == true && _invoiceService is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenSupplierListAsync()
    {
        if (_dialogs is null)
            return;
        await _dialogs.ShowSupplierListAsync();
        if (_invoiceService is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task EditInvoiceAsync(InvoiceDashboardRow? row)
    {
        if (_dialogs is null || row is null || _invoiceService is null)
            return;
        var inv = await _invoiceService.GetInvoiceAsync(row.Id);
        if (inv is null)
            return;
        var ok = await _dialogs.ShowInvoiceEditAsync(inv);
        if (ok == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteInvoiceAsync(InvoiceDashboardRow? row)
    {
        if (_dialogs is null || row is null || _invoiceService is null)
            return;
        if (!await _dialogs.ConfirmAsync("Supprimer la facture", "Supprimer cette facture ?"))
            return;
        await _invoiceService.DeleteInvoiceAsync(row.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ReglerInvoiceAsync(InvoiceDashboardRow? row)
    {
        if (_dialogs is null || row is null || _invoiceService is null)
            return;
        var paidAt = await _dialogs.ShowReglementDialogAsync();
        if (paidAt is null)
            return;
        await _invoiceService.SettleInvoiceAsync(row.Id, paidAt.Value);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task UnsettleInvoiceAsync(InvoiceDashboardRow? row)
    {
        if (_dialogs is null || row is null || _invoiceService is null)
            return;
        if (!await _dialogs.ConfirmAsync(
                "Annuler le règlement",
                "Remettre cette facture en non réglée ? La date de paiement enregistrée sera effacée."))
            return;
        await _invoiceService.UnsettleInvoiceAsync(row.Id);
        await LoadAsync();
    }
}
