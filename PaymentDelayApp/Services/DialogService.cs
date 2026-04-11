using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.ViewModels;
using PaymentDelayApp.Views.Dialogs;

namespace PaymentDelayApp.Services;

public sealed class DialogService : IDialogService
{
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;

    public DialogService(IInvoiceService invoiceService, ISupplierService supplierService)
    {
        _invoiceService = invoiceService;
        _supplierService = supplierService;
    }

    private static Window ResolveOwner(Window? owner)
    {
        if (owner is not null)
            return owner;
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            && d.MainWindow is { } w)
            return w;
        throw new InvalidOperationException("Fenetre principale introuvable.");
    }

    public async Task<bool?> ShowSupplierFormAsync(
        Supplier? existing,
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new SupplierFormWindow();
        var vm = new SupplierFormViewModel(_supplierService, this, win, existing);
        win.DataContext = vm;
        return await win.ShowDialog<bool?>(ResolveOwner(owner));
    }

    public async Task ShowSupplierListAsync(Window? owner = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new SupplierListWindow();
        var vm = new SupplierListViewModel(_supplierService, this, win);
        win.DataContext = vm;
        await win.ShowDialog(ResolveOwner(owner));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<bool?> ShowInvoiceEditAsync(
        Invoice? existing,
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new InvoiceEditWindow();
        var vm = new InvoiceEditViewModel(_invoiceService, _supplierService, this, win, existing);
        win.DataContext = vm;
        return await win.ShowDialog<bool?>(ResolveOwner(owner));
    }

    public async Task<DateTime?> ShowReglementDialogAsync(
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new ReglementConfirmWindow();
        var vm = new ReglementDialogViewModel(win);
        win.DataContext = vm;
        return await win.ShowDialog<DateTime?>(ResolveOwner(owner));
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new ConfirmWindow();
        win.DataContext = new ConfirmDialogViewModel(win, title, message);
        var r = await win.ShowDialog<bool?>(ResolveOwner(owner));
        cancellationToken.ThrowIfCancellationRequested();
        return r == true;
    }

    public async Task ShowMessageAsync(
        string title,
        string message,
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new MessageWindow();
        win.DataContext = new MessageDialogViewModel(win, title, message);
        await win.ShowDialog(ResolveOwner(owner));
        cancellationToken.ThrowIfCancellationRequested();
    }
}
