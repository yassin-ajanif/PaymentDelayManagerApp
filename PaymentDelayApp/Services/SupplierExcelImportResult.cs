namespace PaymentDelayApp.Services;

public sealed class SupplierExcelImportResult
{
    public static SupplierExcelImportResult HeaderFailure(IReadOnlyList<string> messages) =>
        new()
        {
            Success = false,
            InsertedCount = 0,
            UpdatedCount = 0,
            MissingHeaders = messages,
            RowErrors = [],
        };

    public static SupplierExcelImportResult RowFailures(IReadOnlyList<(int ExcelRow, string Message)> rowErrors) =>
        new()
        {
            Success = false,
            InsertedCount = 0,
            UpdatedCount = 0,
            MissingHeaders = [],
            RowErrors = rowErrors,
        };

    public static SupplierExcelImportResult Ok(int insertedCount, int updatedCount) =>
        new()
        {
            Success = true,
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            MissingHeaders = [],
            RowErrors = [],
        };

    public bool Success { get; private init; }
    public int InsertedCount { get; private init; }
    public int UpdatedCount { get; private init; }
    public IReadOnlyList<string> MissingHeaders { get; private init; } = [];
    public IReadOnlyList<(int ExcelRow, string Message)> RowErrors { get; private init; } = [];
}
