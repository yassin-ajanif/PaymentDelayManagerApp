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

    /// <summary>Facture non réglée et reste des jours strictement inférieur au seuil.</summary>
    public bool IsResteJoursAlert => !IsSettled && ResteDesJours < AlertSeuilJours;

    public IBrush ResteForeground =>
        IsResteJoursAlert ? new SolidColorBrush(Color.Parse("#b91c1c")) : Brushes.Black;

    public IBrush RowAlertBackground =>
        IsResteJoursAlert ? new SolidColorBrush(Color.Parse("#fef2f2")) : Brushes.Transparent;

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

    /// <summary>Reste des jours = échéance normale (j) − délai facture (j).</summary>
    public string ResteDisplay => $"{ResteDesJours} j";

    public static InvoiceDashboardRow FromInvoice(Invoice invoice, DateOnly today)
    {
        var norm = EcheanceCalculator.EcheanceNormaleJours(invoice.InvoiceDate, today);
        var resp = EcheanceCalculator.EcheanceRespecteeJours(invoice.EcheanceFactureJours);
        var reste = EcheanceCalculator.ResteDesJours(invoice.InvoiceDate, today, invoice.EcheanceFactureJours);
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
