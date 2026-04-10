using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SupplierListViewModel : ViewModelBase
{
    private readonly ISupplierService _supplierService;
    private readonly IInvoiceService _invoiceService;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    [ObservableProperty]
    private ObservableCollection<SupplierListRow> _supplierRows = [];

    [ObservableProperty]
    private ObservableCollection<SupplierAlertRow> _alertRows = [];

    [ObservableProperty]
    private SupplierListRow? _selectedSupplierRow;

    public SupplierListViewModel(
        ISupplierService supplierService,
        IInvoiceService invoiceService,
        IDialogService dialogs,
        Window window)
    {
        _supplierService = supplierService;
        _invoiceService = invoiceService;
        _dialogs = dialogs;
        _window = window;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var suppliers = await _supplierService.GetSuppliersAsync();
        SupplierRows = new ObservableCollection<SupplierListRow>(
            suppliers.Select(s => new SupplierListRow
            {
                Id = s.Id,
                Name = s.Name,
                Ice = s.Ice,
                FiscalId = s.FiscalId,
                Address = s.Address,
                Activite = s.Activite,
                AlertSeuilJours = s.AlertSeuilJours,
            }));

        var alerts = await _invoiceService.GetAlertInvoicesAsync();
        var rows = new List<SupplierAlertRow>();
        foreach (var inv in alerts)
        {
            var supplier = inv.Supplier ?? await _supplierService.GetSupplierAsync(inv.SupplierId);
            var name = supplier?.Name ?? "-";
            var reste = EcheanceCalculator.ResteDesJours(inv.InvoiceDate, today, inv.EcheanceFactureJours);
            rows.Add(new SupplierAlertRow
            {
                DateFactureDisplay = inv.InvoiceDate.ToString("dd/MM/yyyy"),
                ClientName = name,
                TtcDisplay = inv.TtcAmount.ToString("N2"),
                ResteDisplay = reste + " j",
            });
        }

        AlertRows = new ObservableCollection<SupplierAlertRow>(rows);
    }

    [RelayCommand]
    private async Task EditSupplierAsync()
    {
        if (SelectedSupplierRow is null)
            return;
        var s = await _supplierService.GetSupplierAsync(SelectedSupplierRow.Id);
        if (s is null)
            return;
        var ok = await _dialogs.ShowSupplierFormAsync(s, _window);
        if (ok == true)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteSupplierAsync()
    {
        if (SelectedSupplierRow is null)
            return;
        var msg = "Supprimer " + SelectedSupplierRow.Name + " ?";
        if (!await _dialogs.ConfirmAsync("Supprimer le fournisseur", msg, _window))
            return;

        try
        {
            await _supplierService.DeleteSupplierAsync(SelectedSupplierRow.Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Suppression impossible", ex.Message, _window);
        }
    }

    [RelayCommand]
    private void Close() => _window.Close(true);
}
