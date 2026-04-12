using Avalonia.Media;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.Models;

public sealed class InvoiceDashboardRow
{
    public int Id { get; init; }
    public DateOnly InvoiceDate { get; init; }
    public DateOnly? DeliveryOrServiceDate { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string? Designation { get; init; }
    public decimal TtcAmount { get; init; }
    public int EcheanceFactureJours { get; init; }
    public int EcheanceNormaleJours { get; init; }
    public int EcheanceRespecteeJours { get; init; }
    public int ResteDesJours { get; init; }
    public int AlertSeuilJours { get; init; }
    public bool IsSettled { get; init; }

    public bool CanRegler => !IsSettled;

    public bool CanUnsettle => IsSettled;

    /// <summary>Facture non réglée et reste des jours strictement inférieur au seuil.</summary>
    public bool IsResteJoursAlert => !IsSettled && ResteDesJours < AlertSeuilJours;

    /// <summary>Used in grid cell template (IsVisible); Foreground binding to IBrush was unreliable with DataGrid recycling.</summary>
    public bool IsResteJoursNormalColor => !IsResteJoursAlert;

    /// <summary>Row fill: réglée (vert), alerte reste des jours (rose), sinon transparent.</summary>
    public IBrush RowStripeBackground =>
        IsSettled
            ? new SolidColorBrush(Color.Parse("#d1fae5"))
            : IsResteJoursAlert
                ? new SolidColorBrush(Color.Parse("#fecaca"))
                : Brushes.Transparent;

    public string DateFactureDisplay => InvoiceDate.ToString("dd/MM/yyyy");
    public string DateLivraisonDisplay => DeliveryOrServiceDate?.ToString("dd/MM/yyyy") ?? "—";
    public string TtcDisplay => TtcAmount.ToString("N2");

    /// <summary>Échéance normale (j) = date de facture − aujourd'hui.</summary>
    public string EcheanceNormaleDisplay => $"{EcheanceNormaleJours} j";

    /// <summary>Date limite = date de facture + délai (jours).</summary>
    private string EcheanceLimiteDisplay =>
        EcheanceCalculator.DateEcheanceNormale(InvoiceDate, EcheanceFactureJours).ToString("dd/MM/yyyy");

    public string EcheanceRespecteeDisplay => EcheanceLimiteDisplay;
    public string EcheanceFactureDisplay => EcheanceLimiteDisplay;

    /// <summary>Date d'échéance respectée − aujourd'hui (affichage grille).</summary>
    public string ResteDisplay => $"{ResteDesJours} j";

    public static InvoiceDashboardRow FromInvoice(Invoice invoice, DateOnly today)
    {
        var norm = EcheanceCalculator.EcheanceNormaleJours(invoice.InvoiceDate, today);
        var resp = EcheanceCalculator.EcheanceRespecteeJours(invoice.EcheanceFactureJours);
        var echeanceRespectee = EcheanceCalculator.DateEcheanceNormale(invoice.InvoiceDate, invoice.EcheanceFactureJours);
        var reste = EcheanceCalculator.ResteDesJours(echeanceRespectee, today);
        return new InvoiceDashboardRow
        {
            Id = invoice.Id,
            InvoiceDate = invoice.InvoiceDate,
            DeliveryOrServiceDate = invoice.DeliveryOrServiceDate,
            InvoiceNumber = invoice.InvoiceNumber,
            SupplierName = invoice.Supplier?.Name ?? string.Empty,
            Designation = invoice.Designation,
            TtcAmount = invoice.TtcAmount,
            EcheanceFactureJours = invoice.EcheanceFactureJours,
            EcheanceNormaleJours = norm,
            EcheanceRespecteeJours = resp,
            ResteDesJours = reste,
            AlertSeuilJours = invoice.Supplier?.AlertSeuilJours ?? 7,
            IsSettled = invoice.IsSettled,
        };
    }
}
