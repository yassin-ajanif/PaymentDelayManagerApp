namespace PaymentDelayApp.Services;

/// <summary>
/// Shared layout for supplier list Excel export and import (same column order and header row as the supplier list dialog grid).
/// </summary>
public static class SupplierExcelLayout
{
    public static readonly IReadOnlyList<string> ColumnHeaders =
    [
        "Nom",
        "ICE",
        "IF",
        "Adresse",
        "Activite",
        "Seuil alerte (j)",
    ];

    public const int TitleAndMetaRows = 2;
    public const int HeaderRowNumber = 3;
    public const int FirstDataRowNumber = 4;
    public const int ColumnCount = 6;

    public const int HeaderSearchFirstRow = 1;
    public const int HeaderSearchLastRow = 10;
}
