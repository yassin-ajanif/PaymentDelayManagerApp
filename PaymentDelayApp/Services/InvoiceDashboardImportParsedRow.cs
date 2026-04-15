using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.Services;

/// <summary>One successfully parsed invoice row plus its 1-based Excel row (for errors and ordering).</summary>
internal sealed record InvoiceDashboardImportParsedRow(int ExcelRow, Invoice Invoice);
