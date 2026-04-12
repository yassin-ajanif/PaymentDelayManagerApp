using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    [ObservableProperty]
    private int _scanIntervalMinutes = WatcherSettingsFile.DefaultScanIntervalMinutes;

    public SettingsViewModel()
    {
        _dialogs = null!;
        _window = null!;
    }

    public SettingsViewModel(IDialogService dialogs, Window window)
    {
        _dialogs = dialogs;
        _window = window;
        ReloadFromDisk();
    }

    public void ReloadFromDisk()
    {
        var doc = WatcherSettingsFile.LoadOrDefault();
        ScanIntervalMinutes = doc.ScanIntervalMinutes;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_dialogs is null || _window is null)
            return;
        try
        {
            var clamped = WatcherSettingsFile.ClampScanInterval(ScanIntervalMinutes);
            ScanIntervalMinutes = clamped;
            var existing = WatcherSettingsFile.LoadOrDefault();
            existing.SchemaVersion = 1;
            existing.ScanIntervalMinutes = clamped;
            WatcherSettingsFile.Save(existing);
            await _dialogs.ShowMessageAsync("Paramètres", "Les paramètres du service de surveillance ont été enregistrés.", _window);
            _window.Close();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Erreur", $"Impossible d'enregistrer : {ex.Message}", _window);
        }
    }

    [RelayCommand]
    private void Cancel() => _window?.Close();
}
