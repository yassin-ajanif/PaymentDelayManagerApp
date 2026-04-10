namespace PaymentDelayApp.BusinessLayer.Calculators;

public static class EcheanceCalculator
{
    /// <summary>Date d'échéance normale (jours) = D_F − D_T.</summary>
    public static int EcheanceNormaleJours(DateOnly invoiceDate, DateOnly today) =>
        invoiceDate.DayNumber - today.DayNumber;

    /// <summary>Date limite de paiement au calendrier = D_F + N.</summary>
    public static DateOnly DateEcheanceNormale(DateOnly invoiceDate, int echeanceFactureJours) =>
        invoiceDate.AddDays(echeanceFactureJours);

    /// <summary>Date d'échéance respectée (jours) = N (stored term).</summary>
    public static int EcheanceRespecteeJours(int echeanceFactureJours) => echeanceFactureJours;

    /// <summary>Reste des jours = normale − respectée.</summary>
    public static int ResteDesJours(DateOnly invoiceDate, DateOnly today, int echeanceFactureJours) =>
        EcheanceNormaleJours(invoiceDate, today) - EcheanceRespecteeJours(echeanceFactureJours);
}
