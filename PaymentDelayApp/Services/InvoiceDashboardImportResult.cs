namespace PaymentDelayApp.Services;

public sealed class InvoiceDashboardImportResult
{
    public static InvoiceDashboardImportResult HeaderFailure(IReadOnlyList<string> missingHeaders) =>
        new()
        {
            Success = false,
            ImportedCount = 0,
            MissingHeaders = missingHeaders,
            RowErrors = [],
        };

    public static InvoiceDashboardImportResult RowFailures(IReadOnlyList<(int ExcelRow, string Message)> rowErrors) =>
        new()
        {
            Success = false,
            ImportedCount = 0,
            MissingHeaders = [],
            RowErrors = rowErrors,
        };

    public static InvoiceDashboardImportResult Ok(int importedCount) =>
        new()
        {
            Success = true,
            ImportedCount = importedCount,
            MissingHeaders = [],
            RowErrors = [],
        };

    public bool Success { get; private init; }
    public int ImportedCount { get; private init; }
    public IReadOnlyList<string> MissingHeaders { get; private init; } = [];
    public IReadOnlyList<(int ExcelRow, string Message)> RowErrors { get; private init; } = [];
}
