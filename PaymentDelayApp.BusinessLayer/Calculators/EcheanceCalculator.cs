namespace PaymentDelayApp.BusinessLayer.Calculators;

public static class EcheanceCalculator
{
    /// <summary>Délai utilisé quand l'échéance/facture n'est pas renseignée en base (<c>null</c>).</summary>
    public const int DefaultEcheanceFactureJoursWhenUnset = 60;

    /// <summary>Délai effectif pour date limite et reste : valeur stockée ou <see cref="DefaultEcheanceFactureJoursWhenUnset"/>.</summary>
    public static int EffectiveEcheanceFactureJours(int? stored) =>
        stored ?? DefaultEcheanceFactureJoursWhenUnset;

    /// <summary>Date d'échéance normale (jours) = D_F − D_T.</summary>
    public static int EcheanceNormaleJours(DateOnly invoiceDate, DateOnly today) =>
        invoiceDate.DayNumber - today.DayNumber;

    /// <summary>Date limite de paiement au calendrier = D_F + N.</summary>
    public static DateOnly DateEcheanceNormale(DateOnly invoiceDate, int echeanceFactureJours) =>
        invoiceDate.AddDays(echeanceFactureJours);

    /// <summary>Date d'échéance respectée (jours) = N (stored term).</summary>
    public static int EcheanceRespecteeJours(int echeanceFactureJours) => echeanceFactureJours;

    /// <summary>Reste des jours = date limite de paiement (D_F + N) − aujourd'hui.</summary>
    public static int ResteDesJours(DateOnly invoiceDate, DateOnly today, int echeanceFactureJours)
    {
        var deadline = DateEcheanceNormale(invoiceDate, echeanceFactureJours);
        return deadline.DayNumber - today.DayNumber;
    }
}
