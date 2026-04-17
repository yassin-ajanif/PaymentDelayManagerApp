using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SupplierFormViewModel : ViewModelBase
{
    private readonly ISupplierService _suppliers;
    private readonly IDialogService _dialogs;
    private readonly Window _window;
    private readonly int? _existingId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _ice;

    [ObservableProperty]
    private string? _fiscalId;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _activite;

    [ObservableProperty]
    private int _alertSeuilJours = 7;

    public SupplierFormViewModel(
        ISupplierService suppliers,
        IDialogService dialogs,
        Window window,
        Supplier? existing)
    {
        _suppliers = suppliers;
        _dialogs = dialogs;
        _window = window;
        if (existing is not null)
        {
            _existingId = existing.Id;
            Name = existing.Name;
            Ice = existing.Ice;
            FiscalId = existing.FiscalId;
            Address = existing.Address;
            Activite = existing.Activite;
            AlertSeuilJours = existing.AlertSeuilJours;
        }
    }

    public string WindowTitle => _existingId is null ? "Ajouter fournisseur" : "Modifier fournisseur";

    [RelayCommand]
    private async Task SaveAsync()
    {
        var supplier = new Supplier
        {
            Id = _existingId ?? 0,
            Name = Name ?? string.Empty,
            Ice = Ice,
            FiscalId = FiscalId,
            Address = Address,
            Activite = Activite,
            AlertSeuilJours = AlertSeuilJours,
        };

        try
        {
            await _suppliers.SaveSupplierAsync(supplier);
            _window.Close(true);
        }
        catch (InvalidOperationException ex)
        {
            await _dialogs.ShowMessageAsync("Validation", ex.Message, _window);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Erreur", ex.Message, _window);
        }
    }

    [RelayCommand]
    private void Cancel() => _window.Close(false);
}
