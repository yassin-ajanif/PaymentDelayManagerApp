using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Models;
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
        var vm = new SupplierListViewModel(_supplierService, _invoiceService, this, win);
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

    public async Task ShowPaymentAlertAsync(
        IReadOnlyList<PaymentAlertLine> lines,
        Window? owner = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var win = new PaymentAlertWindow();
        win.DataContext = new PaymentAlertViewModel(win, lines);
        await win.ShowDialog(ResolveOwner(owner));
        cancellationToken.ThrowIfCancellationRequested();
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

    public async Task ShowStartupPaymentAlertsIfNeededAsync(CancellationToken cancellationToken = default)
    {
        var alerts = await _invoiceService.GetAlertInvoicesAsync(cancellationToken);
        if (alerts.Count == 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lines = new List<PaymentAlertLine>();
        foreach (var inv in alerts)
        {
            var supplier = inv.Supplier ?? await _supplierService.GetSupplierAsync(inv.SupplierId, cancellationToken);
            var name = supplier?.Name ?? "-";
            var reste = EcheanceCalculator.ResteDesJours(inv.InvoiceDate, today, inv.EcheanceFactureJours);
            lines.Add(new PaymentAlertLine
            {
                SupplierName = name,
                InvoiceNumber = inv.InvoiceNumber,
                DateFactureDisplay = inv.InvoiceDate.ToString("dd/MM/yyyy"),
                TtcDisplay = inv.TtcAmount.ToString("N2"),
                ResteDesJours = reste,
            });
        }

        await ShowPaymentAlertAsync(lines, null, cancellationToken);
    }
}
