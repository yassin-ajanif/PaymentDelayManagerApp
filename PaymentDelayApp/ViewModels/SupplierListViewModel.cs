using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SupplierListViewModel : ViewModelBase
{
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    [ObservableProperty]
    private ObservableCollection<SupplierListRow> _supplierRows = [];

    [ObservableProperty]
    private SupplierListRow? _selectedSupplierRow;

    public SupplierListViewModel(
        ISupplierService supplierService,
        IDialogService dialogs,
        Window window)
    {
        _supplierService = supplierService;
        _dialogs = dialogs;
        _window = window;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
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
    private async Task DeleteSupplierAsync(SupplierListRow? row)
    {
        var target = row ?? SelectedSupplierRow;
        if (target is null)
            return;
        SelectedSupplierRow = target;
        var msg = "Supprimer " + target.Name + " ?";
        if (!await _dialogs.ConfirmAsync("Supprimer le fournisseur", msg, _window))
            return;

        try
        {
            await _supplierService.DeleteSupplierAsync(target.Id);
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
