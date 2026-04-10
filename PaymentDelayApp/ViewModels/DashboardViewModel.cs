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
    private InvoiceDashboardRow? _selectedRow;

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
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        IEnumerable<InvoiceDashboardRow> src = _allRows;
        if (q.Length > 0)
        {
            src = _allRows.Where(r =>
                r.InvoiceNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.SupplierName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Rows = new ObservableCollection<InvoiceDashboardRow>(src);
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
}
