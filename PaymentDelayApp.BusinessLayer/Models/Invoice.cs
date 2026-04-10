namespace PaymentDelayApp.BusinessLayer.Models;

public class Invoice
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DeliveryOrServiceDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public decimal TtcAmount { get; set; }
    public int EcheanceFactureJours { get; set; } = 60;
    public bool IsSettled { get; set; }
    public bool IsPaymentAlert { get; set; }
    public DateTime? PaidAt { get; set; }
}
