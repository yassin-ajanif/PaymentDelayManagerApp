using Avalonia.Controls;
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
}
