using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class InvoiceEditViewModel : ViewModelBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogs;
    private readonly Window _window;
    private readonly int _invoiceId;
    private bool _preserveSettled;
    private DateTime? _preservePaidAt;
    private bool _preservePaymentAlert;

    [ObservableProperty]
    private ObservableCollection<Supplier> _suppliers = [];

    [ObservableProperty]
    private Supplier? _selectedSupplier;

    [ObservableProperty]
    private DateTimeOffset? _invoiceDateUi;

    [ObservableProperty]
    private DateTimeOffset? _deliveryDateUi;

    [ObservableProperty]
    private string _invoiceNumber = string.Empty;

    [ObservableProperty]
    private string? _designation;

    [ObservableProperty]
    private decimal _ttcAmount;

    [ObservableProperty]
    private int _echeanceFactureJours = 60;

    /// <summary>DatePicker bounds: only the current calendar year.</summary>
    public DateTimeOffset InvoiceDatePickerMinYear { get; } =
        new(new DateTime(DateTime.Today.Year, 1, 1, 0, 0, 0, DateTimeKind.Local));

    public DateTimeOffset InvoiceDatePickerMaxYear { get; } =
        new(new DateTime(DateTime.Today.Year, 12, 31, 0, 0, 0, DateTimeKind.Local));

    public InvoiceEditViewModel(
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IDialogService dialogs,
        Window window,
        Invoice? existing)
    {
        _invoiceService = invoiceService;
        _supplierService = supplierService;
        _dialogs = dialogs;
        _window = window;
        _invoiceId = existing?.Id ?? 0;

        if (existing is not null)
        {
            var y = DateTime.Today.Year;
            InvoiceDateUi = ToDateTimeOffset(
                EnsureInCalendarYear(existing.InvoiceDate, y).ToDateTime(TimeOnly.MinValue));
            DeliveryDateUi = existing.DeliveryOrServiceDate is { } del
                ? ToDateTimeOffset(EnsureInCalendarYear(del, y).ToDateTime(TimeOnly.MinValue))
                : null;
            InvoiceNumber = existing.InvoiceNumber;
            Designation = existing.Designation;
            TtcAmount = existing.TtcAmount;
            EcheanceFactureJours = existing.EcheanceFactureJours;
            _preserveSettled = existing.IsSettled;
            _preservePaidAt = existing.PaidAt;
            _preservePaymentAlert = existing.IsPaymentAlert;
        }
        else
        {
            InvoiceDateUi = ToDateTimeOffset(DateTime.Today);
        }

        _ = LoadSuppliersAsync(existing);
    }

    public string WindowTitle => _invoiceId == 0 ? "Nouvelle facture" : "Modifier la facture";

    /// <summary>Échéance normale (j) = date de facture − aujourd'hui.</summary>
    public string EcheanceNormaleDisplay => FormatJoursNormale(ComputeNormaleJours());

    public string EcheanceRespecteeDisplay => EcheanceLimiteDisplay;

    /// <summary>Reste des jours = délai facture (j) − normale (j) — jours jusqu'à la date limite.</summary>
    public string ResteDisplay => FormatJoursNormale(ComputeResteJours());

    /// <summary>Date limite = date de facture + délai (jours).</summary>
    private string EcheanceLimiteDisplay
    {
        get
        {
            var d = InvoiceDateUi;
            if (d is null)
                return "—";
            var inv = DateOnly.FromDateTime(d.Value.LocalDateTime.Date);
            return EcheanceCalculator.DateEcheanceNormale(inv, EcheanceFactureJours).ToString("dd/MM/yyyy");
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime date) =>
        new(DateTime.SpecifyKind(date.Date, DateTimeKind.Local));

    /// <summary>If the date is not in <paramref name="year"/>, project month/day into that year (clamp day).</summary>
    private static DateOnly EnsureInCalendarYear(DateOnly d, int year)
    {
        if (d.Year == year)
            return d;
        var m = d.Month;
        var maxDay = DateTime.DaysInMonth(year, m);
        var day = Math.Min(d.Day, maxDay);
        return new DateOnly(year, m, day);
    }

    /// <summary>Date facture − aujourd'hui (jours entiers signés).</summary>
    private int? ComputeNormaleJours()
    {
        var d = InvoiceDateUi;
        if (d is null)
            return null;
        var inv = DateOnly.FromDateTime(d.Value.LocalDateTime.Date);
        var today = DateOnly.FromDateTime(DateTime.Today);
        return EcheanceCalculator.EcheanceNormaleJours(inv, today);
    }

    private static string FormatJoursNormale(int? j) => j is null ? "—" : $"{j} j";

    private int? ComputeResteJours()
    {
        var n = ComputeNormaleJours();
        if (n is null)
            return null;
        return EcheanceFactureJours - n.Value;
    }

    partial void OnInvoiceDateUiChanged(DateTimeOffset? value) => RefreshComputed();
    partial void OnEcheanceFactureJoursChanged(int value) => RefreshComputed();

    private void RefreshComputed()
    {
        OnPropertyChanged(nameof(EcheanceNormaleDisplay));
        OnPropertyChanged(nameof(EcheanceRespecteeDisplay));
        OnPropertyChanged(nameof(ResteDisplay));
    }

    private async Task LoadSuppliersAsync(Invoice? existing)
    {
        var list = await _supplierService.GetSuppliersAsync();
        Suppliers = new ObservableCollection<Supplier>(list);
        if (existing is not null)
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == existing.SupplierId);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSupplier is null)
        {
            await _dialogs.ShowMessageAsync("Validation", "Choisissez un fournisseur.", _window);
            return;
        }

        if (InvoiceDateUi is null)
        {
            await _dialogs.ShowMessageAsync("Validation", "Indiquez la date de facture.", _window);
            return;
        }

        var invoiceDateOnly = DateOnly.FromDateTime(InvoiceDateUi.Value.LocalDateTime.Date);
        var calendarYear = DateTime.Today.Year;
        if (invoiceDateOnly.Year != calendarYear)
        {
            await _dialogs.ShowMessageAsync(
                "Validation",
                $"La date de facture doit être dans l'année {calendarYear} (année en cours).",
                _window);
            return;
        }

        if (DeliveryDateUi is { } delUi)
        {
            var delOnly = DateOnly.FromDateTime(delUi.LocalDateTime.Date);
            if (delOnly.Year != calendarYear)
            {
                await _dialogs.ShowMessageAsync(
                    "Validation",
                    $"La date de livraison ou prestation doit être dans l'année {calendarYear} (année en cours).",
                    _window);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            await _dialogs.ShowMessageAsync("Validation", "Indiquez le numéro de facture.", _window);
            return;
        }

        if (EcheanceFactureJours > 120)
        {
            await _dialogs.ShowMessageAsync("Validation", "La date d'échéance/facture ne peut pas dépasser 120 jours.", _window);
            return;
        }

        var invoice = new Invoice
        {
            Id = _invoiceId,
            SupplierId = SelectedSupplier.Id,
            InvoiceDate = invoiceDateOnly,
            DeliveryOrServiceDate = DeliveryDateUi is { } dd
                ? DateOnly.FromDateTime(dd.LocalDateTime.Date)
                : null,
            InvoiceNumber = InvoiceNumber.Trim(),
            Designation = string.IsNullOrWhiteSpace(Designation) ? null : Designation.Trim(),
            TtcAmount = TtcAmount,
            EcheanceFactureJours = EcheanceFactureJours,
            IsSettled = _preserveSettled,
            PaidAt = _preservePaidAt,
            IsPaymentAlert = _preservePaymentAlert,
        };

        try
        {
            await _invoiceService.SaveInvoiceAsync(invoice);
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
