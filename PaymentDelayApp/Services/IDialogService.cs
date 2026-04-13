using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.Services;

public interface IDialogService
{
    Task<bool?> ShowSupplierFormAsync(Supplier? existing, Window? owner = null, CancellationToken cancellationToken = default);

    Task ShowSupplierListAsync(Window? owner = null, CancellationToken cancellationToken = default);

    Task<bool?> ShowInvoiceEditAsync(Invoice? existing, Window? owner = null, CancellationToken cancellationToken = default);

    Task<DateTime?> ShowReglementDialogAsync(Window? owner = null, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(string title, string message, Window? owner = null, CancellationToken cancellationToken = default);

    Task ShowMessageAsync(string title, string message, Window? owner = null, CancellationToken cancellationToken = default);

    Task ShowSettingsAsync(Window? owner = null, CancellationToken cancellationToken = default);

    Task ShowBackupSettingsAsync(Window? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Save-file picker for exports; returns null if cancelled or unavailable.</summary>
    Task<IStorageFile?> PickSaveExportFileAsync(
        string suggestedFileName,
        IReadOnlyList<FilePickerFileType> fileTypes,
        Window? owner = null,
        CancellationToken cancellationToken = default);

    /// <summary>Open-file picker for Excel import; returns null if cancelled or unavailable.</summary>
    Task<IStorageFile?> PickOpenImportExcelFileAsync(
        Window? owner = null,
        CancellationToken cancellationToken = default);
}
