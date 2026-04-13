using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class SupplierListViewModel : ViewModelBase
{
    private static readonly FilePickerFileType[] ExcelSaveTypes =
    [
        new("Excel") { Patterns = ["*.xlsx"], MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] },
    ];

    private readonly ISupplierService _supplierService;
    private readonly ISupplierExcelService _supplierExcel;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    [ObservableProperty]
    private ObservableCollection<SupplierListRow> _supplierRows = [];

    [ObservableProperty]
    private SupplierListRow? _selectedSupplierRow;

    public SupplierListViewModel(
        ISupplierService supplierService,
        ISupplierExcelService supplierExcel,
        IDialogService dialogs,
        Window window)
    {
        _supplierService = supplierService;
        _supplierExcel = supplierExcel;
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

    [RelayCommand]
    private async Task ExportExcelAsync(CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetSuppliersAsync(cancellationToken);
        if (suppliers.Count == 0)
        {
            await _dialogs.ShowMessageAsync("Export", "Aucun fournisseur à exporter.", _window);
            return;
        }

        var suggested = $"fournisseurs_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        var file = await _dialogs.PickSaveExportFileAsync(suggested, ExcelSaveTypes, _window, cancellationToken);
        if (file is null)
            return;

        var title = "Liste des fournisseurs";
        var stamp = $"Exporté le {DateTime.Now:dd/MM/yyyy} à {DateTime.Now:HH:mm}";
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await _supplierExcel.WriteExcelAsync(suppliers, stream, title, stamp, cancellationToken);
            await _dialogs.ShowMessageAsync("Export", "Fichier Excel enregistré.", _window);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Export", $"Erreur lors de l'export : {ex.Message}", _window);
        }
    }

    [RelayCommand]
    private async Task ImportExcelAsync(CancellationToken cancellationToken)
    {
        var file = await _dialogs.PickOpenImportExcelFileAsync(_window, cancellationToken);
        if (file is null)
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            var result = await _supplierExcel.ImportFromExcelAsync(stream, cancellationToken);
            await _dialogs.ShowMessageAsync("Import Excel", FormatSupplierImportResultMessage(result), _window);
            if (result.Success)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Import Excel", $"Erreur : {ex.Message}", _window);
        }
    }

    private static string FormatSupplierImportResultMessage(SupplierExcelImportResult r)
    {
        if (r.MissingHeaders.Count > 0)
            return string.Join(Environment.NewLine, r.MissingHeaders);
        if (!r.Success)
        {
            var lines = r.RowErrors.Take(50).Select(e => $"Ligne {e.ExcelRow} : {e.Message}");
            var body = string.Join(Environment.NewLine, lines);
            if (r.RowErrors.Count > 50)
                body += $"{Environment.NewLine}… et {r.RowErrors.Count - 50} autre(s) erreur(s).";
            return $"Import annulé — aucun fournisseur enregistré.{Environment.NewLine}{Environment.NewLine}{r.RowErrors.Count} erreur(s) :{Environment.NewLine}{body}";
        }

        if (r.InsertedCount == 0 && r.UpdatedCount == 0)
            return "Aucune ligne de données à importer (fichier vide ou uniquement des lignes vides).";

        return $"{r.InsertedCount} fournisseur(s) créé(s), {r.UpdatedCount} fournisseur(s) mis à jour.";
    }
}
