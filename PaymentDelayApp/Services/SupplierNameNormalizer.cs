namespace PaymentDelayApp.Services;

/// <summary>
/// Single place for supplier name trimming used by supplier Excel import and invoice dashboard import (Fournisseur resolution).
/// </summary>
public static class SupplierNameNormalizer
{
    public static string NormalizeKey(string? name) => (name ?? string.Empty).Trim();
}
