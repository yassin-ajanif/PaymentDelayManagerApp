using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IDialogService _dialogs;
    private List<InvoiceDashboardRow> _allRows = [];

    [ObservableProperty]
    private ObservableCollection<InvoiceDashboardRow> _rows = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

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

    public DashboardViewModel()
    {
        _invoiceService = null!;
        _dialogs = null!;
    }

    public DashboardViewModel(
        IInvoiceService invoiceService,
        IDialogService dialogs)
    {
        _invoiceService = invoiceService;
        _dialogs = dialogs;
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

        Rows = new ObservableCollection<InvoiceDashboardRow>(src);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (_dialogs is null)
            return;
        await _dialogs.ShowSettingsAsync();
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
