namespace PaymentDelayApp.Models;

public sealed class SupplierListRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Ice { get; init; }
    public string? FiscalId { get; init; }
    public string? Address { get; init; }
    public string? Activite { get; init; }
    public int AlertSeuilJours { get; init; }
}
