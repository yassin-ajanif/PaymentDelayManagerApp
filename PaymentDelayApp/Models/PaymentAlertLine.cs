namespace PaymentDelayApp.Models;

public sealed class PaymentAlertLine
{
    public string SupplierName { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string DateFactureDisplay { get; init; } = string.Empty;
    public string TtcDisplay { get; init; } = string.Empty;
    public int ResteDesJours { get; init; }
    public string ResteDisplay => $"{ResteDesJours} j";
}
