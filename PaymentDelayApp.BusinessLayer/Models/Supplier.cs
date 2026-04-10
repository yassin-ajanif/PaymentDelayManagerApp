namespace PaymentDelayApp.BusinessLayer.Models;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ice { get; set; }
    public string? FiscalId { get; set; }
    public string? Address { get; set; }
    public string? Activite { get; set; }
    public int AlertSeuilJours { get; set; } = 7;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
