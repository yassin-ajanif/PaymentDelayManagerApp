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
    private const string EcheanceAvantFactureMessage =
        "La date d'échéance/facture ne peut pas être antérieure à la date de facture.";

    private const string EcheanceFacturePlageMessage =
        "La date d'échéance/facture doit être entre 0 et 120 jours après la date de facture.";

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

    /// <summary>Null = échéance/facture non renseignée (effacée ou jamais choisie).</summary>
    [ObservableProperty]
    private int? _echeanceFactureJours;

    /// <summary>Calendar deadline = date de facture + <see cref="EcheanceFactureJours"/> (stored value remains days in DB).</summary>
    [ObservableProperty]
    private DateTimeOffset? _echeanceFactureDateUi;

    private bool _suppressEcheanceDateSync;

    /// <summary>DatePicker bounds: only the current calendar year.</summary>
    public DateTimeOffset InvoiceDatePickerMinYear { get; } =
        new(new DateTime(DateTime.Today.Year, 1, 1, 0, 0, 0, DateTimeKind.Local));

    public DateTimeOffset InvoiceDatePickerMaxYear { get; } =
        new(new DateTime(DateTime.Today.Year, 12, 31, 0, 0, 0, DateTimeKind.Local));

    /// <summary>Deadline picker: from invoice year Jan 1 through end of following year (invoice + up to 120 days).</summary>
    public DateTimeOffset EcheanceFactureDatePickerMinYear =>
        new(new DateTime(InvoiceDateUi?.Year ?? DateTime.Today.Year, 1, 1, 0, 0, 0, DateTimeKind.Local));

    public DateTimeOffset EcheanceFactureDatePickerMaxYear =>
        new(new DateTime((InvoiceDateUi?.Year ?? DateTime.Today.Year) + 1, 12, 31, 0, 0, 0, DateTimeKind.Local));

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
            EcheanceFactureJours = existing.EcheanceFactureJours;
            InvoiceDateUi = ToDateTimeOffset(
                EnsureInCalendarYear(existing.InvoiceDate, y).ToDateTime(TimeOnly.MinValue));
            DeliveryDateUi = existing.DeliveryOrServiceDate is { } del
                ? ToDateTimeOffset(EnsureInCalendarYear(del, y).ToDateTime(TimeOnly.MinValue))
                : null;
            InvoiceNumber = existing.InvoiceNumber;
            Designation = existing.Designation;
            TtcAmount = existing.TtcAmount;
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

    public string EcheanceRespecteeDisplay => EcheanceLimiteDisplay;

    /// <summary>Reste des jours jusqu'à la date limite ; si échéance/facture non renseignée, délai par défaut 60 j.</summary>
    public string ResteDisplay => FormatJoursNormale(ComputeResteJours());

    /// <summary>
    /// Date limite « échéance respectée » : date picker si présente, sinon facture + jours stockés,
    /// sinon facture + <see cref="EcheanceCalculator.DefaultEcheanceFactureJoursWhenUnset"/> j.
    /// </summary>
    private string EcheanceLimiteDisplay
    {
        get
        {
            if (InvoiceDateUi is not { } invDto)
                return "—";
            var inv = DateOnly.FromDateTime(invDto.LocalDateTime.Date);
            if (EcheanceFactureDateUi is { } limDto)
                return DateOnly.FromDateTime(limDto.LocalDateTime.Date).ToString("dd/MM/yyyy");
            if (EcheanceFactureJours is int storedJours)
                return EcheanceCalculator.DateEcheanceNormale(inv, storedJours).ToString("dd/MM/yyyy");
            var term = EcheanceCalculator.EffectiveEcheanceFactureJours(EcheanceFactureJours);
            return EcheanceCalculator.DateEcheanceNormale(inv, term).ToString("dd/MM/yyyy");
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

    private static string FormatJoursNormale(int? j) => j is null ? "—" : $"{j} j";

    private int? ComputeResteJours()
    {
        if (InvoiceDateUi is not { } invDto)
            return null;
        var inv = DateOnly.FromDateTime(invDto.LocalDateTime.Date);
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (EcheanceFactureDateUi is { } limDto)
        {
            var deadline = DateOnly.FromDateTime(limDto.LocalDateTime.Date);
            return deadline.DayNumber - today.DayNumber;
        }

        if (EcheanceFactureJours is int t)
            return EcheanceCalculator.ResteDesJours(inv, today, t);
        var term = EcheanceCalculator.EffectiveEcheanceFactureJours(EcheanceFactureJours);
        return EcheanceCalculator.ResteDesJours(inv, today, term);
    }

    partial void OnInvoiceDateUiChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(EcheanceFactureDatePickerMinYear));
        OnPropertyChanged(nameof(EcheanceFactureDatePickerMaxYear));
        RefreshEcheanceFactureDateFromJours();
        RefreshComputed();
    }

    partial void OnEcheanceFactureJoursChanged(int? value)
    {
        if (_suppressEcheanceDateSync)
            return;
        RefreshEcheanceFactureDateFromJours();
        RefreshComputed();
    }

    partial void OnEcheanceFactureDateUiChanged(DateTimeOffset? value)
    {
        if (_suppressEcheanceDateSync)
            return;
        if (value is null)
        {
            EcheanceFactureJours = null;
            RefreshComputed();
            return;
        }

        if (InvoiceDateUi is null)
            return;
        var inv = DateOnly.FromDateTime(InvoiceDateUi.Value.LocalDateTime.Date);
        var deadline = DateOnly.FromDateTime(value.Value.LocalDateTime.Date);
        var jours = deadline.DayNumber - inv.DayNumber;
        if (!EcheanceFactureJours.HasValue || EcheanceFactureJours.Value != jours)
            EcheanceFactureJours = jours;

        RefreshComputed();
    }

    private void RefreshEcheanceFactureDateFromJours()
    {
        if (InvoiceDateUi is null)
            return;
        _suppressEcheanceDateSync = true;
        if (!EcheanceFactureJours.HasValue)
            EcheanceFactureDateUi = null;
        else
        {
            var inv = DateOnly.FromDateTime(InvoiceDateUi.Value.LocalDateTime.Date);
            EcheanceFactureDateUi = ToDateTimeOffset(
                EcheanceCalculator.DateEcheanceNormale(inv, EcheanceFactureJours.Value).ToDateTime(TimeOnly.MinValue));
        }

        _suppressEcheanceDateSync = false;
    }

    private void RefreshComputed()
    {
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

            if (delOnly < invoiceDateOnly)
            {
                await _dialogs.ShowMessageAsync(
                    "Validation",
                    "La date de livraison ou prestation doit être le même jour ou après la date de facture.",
                    _window);
                return;
            }
        }

        if (TtcAmount < 1m)
        {
            await _dialogs.ShowMessageAsync(
                "Validation",
                "Le TTC doit être supérieur ou égal à 1.",
                _window);
            return;
        }

        if (string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            await _dialogs.ShowMessageAsync("Validation", "Indiquez le numéro de facture.", _window);
            return;
        }

        int? echeanceJours;
        if (EcheanceFactureDateUi is not null)
        {
            var deadline = DateOnly.FromDateTime(EcheanceFactureDateUi.Value.LocalDateTime.Date);
            var joursFromDates = deadline.DayNumber - invoiceDateOnly.DayNumber;
            if (joursFromDates < 0)
            {
                await _dialogs.ShowMessageAsync("Validation", EcheanceAvantFactureMessage, _window);
                return;
            }

            if (joursFromDates > 120)
            {
                await _dialogs.ShowMessageAsync("Validation", EcheanceFacturePlageMessage, _window);
                return;
            }

            echeanceJours = joursFromDates;
        }
        else if (EcheanceFactureJours is int ej)
        {
            if (ej < 0)
            {
                await _dialogs.ShowMessageAsync("Validation", EcheanceAvantFactureMessage, _window);
                return;
            }

            if (ej > 120)
            {
                await _dialogs.ShowMessageAsync("Validation", EcheanceFacturePlageMessage, _window);
                return;
            }

            echeanceJours = ej;
        }
        else
            echeanceJours = null;

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
            EcheanceFactureJours = echeanceJours,
            IsSettled = _preserveSettled,
            PaidAt = _preservePaidAt,
            IsPaymentAlert = _preservePaymentAlert,
        };

        try
        {
            await _invoiceService.SaveInvoiceAsync(invoice);
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

    /// <summary>Remet la date d'échéance/facture à vide (le DatePicker Avalonia n'a pas d'action « effacer » intégrée).</summary>
    [RelayCommand]
    private void ClearEcheanceFactureDate()
    {
        if (!EcheanceFactureJours.HasValue && EcheanceFactureDateUi is null)
            return;
        EcheanceFactureJours = null;
    }

    [RelayCommand]
    private void ClearDeliveryDate()
    {
        if (DeliveryDateUi is null)
            return;
        DeliveryDateUi = null;
    }

    [RelayCommand]
    private void Cancel() => _window.Close(false);
}
