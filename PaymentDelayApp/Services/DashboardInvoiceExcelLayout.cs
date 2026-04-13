namespace PaymentDelayApp.Services;

/// <summary>
/// Shared layout for dashboard invoice Excel export and import (same column order and header row).
/// </summary>
public static class DashboardInvoiceExcelLayout
{
    /// <summary>Placeholder used in export for missing optional dates.</summary>
    public const string EmptyCellDisplay = "—";

    public static readonly IReadOnlyList<string> ColumnHeaders =
    [
        "Date de facture",
        "Date de livraison ou préstation",
        "N° de Facture",
        "Fournisseur",
        "Désignation",
        "TTC",
        "Date d'échéance respectée",
        "Reste des jours",
        "Date d'échéance/facture",
    ];

    public const int TitleAndMetaRows = 2;
    public const int HeaderRowNumber = 3;
    public const int FirstDataRowNumber = 4;
    public const int ColumnCount = 9;

    /// <summary>Scan these 1-based worksheet rows (inclusive) to find the header row.</summary>
    public const int HeaderSearchFirstRow = 1;
    public const int HeaderSearchLastRow = 10;
}
