using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SupplierFormViewModel : ViewModelBase
{
    private const int MinAlertSeuilJours = 1;
    private const int MaxAlertSeuilJours = 120;

    private readonly ISupplierService _suppliers;
    private readonly IDialogService _dialogs;
    private readonly Window _window;
    private readonly int? _existingId;

    private bool _initialized;
    private int? _lastOutOfRangeAlertSeuilJours;

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

        _initialized = true;
    }

    public string WindowTitle => _existingId is null ? "Ajouter fournisseur" : "Modifier fournisseur";

    partial void OnAlertSeuilJoursChanged(int value)
    {
        if (value is >= MinAlertSeuilJours and <= MaxAlertSeuilJours)
        {
            _lastOutOfRangeAlertSeuilJours = null;
            return;
        }

        if (!_initialized)
            return;

        if (_lastOutOfRangeAlertSeuilJours == value)
            return;
        _lastOutOfRangeAlertSeuilJours = value;

        _ = ShowAlertSeuilOutOfRangeAsync();
    }

    private async Task ShowAlertSeuilOutOfRangeAsync()
    {
        try
        {
            await _dialogs.ShowMessageAsync(
                "Validation",
                $"Le seuil d'alerte doit être entre {MinAlertSeuilJours} et {MaxAlertSeuilJours} jours.",
                _window);
        }
        catch
        {
            // best-effort UI feedback
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogs.ShowMessageAsync("Validation", "Le nom du fournisseur est obligatoire.", _window);
            return;
        }

        if (AlertSeuilJours is < MinAlertSeuilJours or > MaxAlertSeuilJours)
        {
            await _dialogs.ShowMessageAsync(
                "Validation",
                $"Le seuil d'alerte doit être entre {MinAlertSeuilJours} et {MaxAlertSeuilJours} jours.",
                _window);
            return;
        }

        var supplier = new Supplier
        {
            Id = _existingId ?? 0,
            Name = Name.Trim(),
            Ice = string.IsNullOrWhiteSpace(Ice) ? null : Ice.Trim(),
            FiscalId = string.IsNullOrWhiteSpace(FiscalId) ? null : FiscalId.Trim(),
            Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
            Activite = string.IsNullOrWhiteSpace(Activite) ? null : Activite.Trim(),
            AlertSeuilJours = AlertSeuilJours,
        };

        try
        {
            await _suppliers.SaveSupplierAsync(supplier);
            _window.Close(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Erreur", ex.Message, _window);
        }
    }

    [RelayCommand]
    private void Cancel() => _window.Close(false);
}
