using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.BusinessLayer.Calculators;

public static class PaymentAlertEvaluator
{
    public static bool IsPaymentAlert(Invoice invoice, Supplier supplier, DateOnly today)
    {
        if (invoice.IsSettled)
            return false;
        var term = EcheanceCalculator.EffectiveEcheanceFactureJours(invoice.EcheanceFactureJours);
        var reste = EcheanceCalculator.ResteDesJours(invoice.InvoiceDate, today, term);
        return reste < supplier.AlertSeuilJours;
    }

    public static void ApplyPaymentAlertFlag(Invoice invoice, Supplier supplier, DateOnly today) =>
        invoice.IsPaymentAlert = IsPaymentAlert(invoice, supplier, today);
}
