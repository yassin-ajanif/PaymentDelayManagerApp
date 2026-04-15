using Microsoft.Data.Sqlite;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Models;

namespace PaymentDelayApp.Services;

public sealed class DatabaseBackupService : IBackupService
{
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IInvoiceDashboardExportService _invoiceExportService;
    private readonly ISupplierExcelService _supplierExcelService;

    public DatabaseBackupService(
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IInvoiceDashboardExportService invoiceExportService,
        ISupplierExcelService supplierExcelService)
    {
        _invoiceService = invoiceService;
        _supplierService = supplierService;
        _invoiceExportService = invoiceExportService;
        _supplierExcelService = supplierExcelService;
    }

    public async Task CreateBackupAsync(
        string sourceDatabasePath,
        string backupsDirectory,
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupsDirectory);
        retentionDays = BackupSettingsFile.ClampRetentionDays(retentionDays);

        if (!File.Exists(sourceDatabasePath))
            throw new FileNotFoundException("Fichier base introuvable.", sourceDatabasePath);

        Directory.CreateDirectory(backupsDirectory);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var bundleFolder = Path.Combine(backupsDirectory, $"backup_{stamp}");
        if (Directory.Exists(bundleFolder))
            bundleFolder = Path.Combine(backupsDirectory, $"backup_{stamp}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundleFolder);

        var dbBackupPath = Path.Combine(bundleFolder, $"app_backup_{stamp}.db");

        var sourceCs = new SqliteConnectionStringBuilder { DataSource = sourceDatabasePath }.ToString();
        var destCs = new SqliteConnectionStringBuilder { DataSource = dbBackupPath }.ToString();

        await using var source = new SqliteConnection(sourceCs);
        await source.OpenAsync(cancellationToken);
        await using var dest = new SqliteConnection(destCs);
        await dest.OpenAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        // Single-shot SQLite copy; not cancelled mid-flight (would require paged backup + shared lifetime).
        source.BackupDatabase(dest);
        cancellationToken.ThrowIfCancellationRequested();

        await ExportInvoicesAsync(bundleFolder, stamp, cancellationToken).ConfigureAwait(false);
        await ExportSuppliersAsync(bundleFolder, stamp, cancellationToken).ConfigureAwait(false);

        await PruneBackupsAsync(backupsDirectory, retentionDays, cancellationToken);
    }

    private async Task ExportInvoicesAsync(string bundleFolder, string stamp, CancellationToken cancellationToken)
    {
        var invoices = await _invoiceService.GetInvoicesAsync(cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = invoices.Select(i => InvoiceDashboardRow.FromInvoice(i, today)).ToList();
        var title = "Sauvegarde - Factures";
        var timestampLine = $"Export de sauvegarde {stamp}";
        var filePath = Path.Combine(bundleFolder, $"invoices_{stamp}.xlsx");
        await using var stream = File.Create(filePath);
        await _invoiceExportService.WriteExcelAsync(rows, stream, title, timestampLine, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExportSuppliersAsync(string bundleFolder, string stamp, CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetSuppliersAsync(cancellationToken).ConfigureAwait(false);
        var title = "Sauvegarde - Fournisseurs";
        var timestampLine = $"Export de sauvegarde {stamp}";
        var filePath = Path.Combine(bundleFolder, $"suppliers_{stamp}.xlsx");
        await using var stream = File.Create(filePath);
        await _supplierExcelService.WriteExcelAsync(suppliers, stream, title, timestampLine, cancellationToken).ConfigureAwait(false);
    }

    public Task PruneBackupsAsync(
        string backupsDirectory,
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupsDirectory);
        retentionDays = BackupSettingsFile.ClampRetentionDays(retentionDays);

        if (!Directory.Exists(backupsDirectory))
            return Task.CompletedTask;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        foreach (var path in Directory.EnumerateDirectories(backupsDirectory, "backup_*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (Directory.GetLastWriteTimeUtc(path) < cutoff)
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // skip files in use or permission issues
            }
        }

        // Legacy cleanup for older flat backup files.
        foreach (var path in Directory.EnumerateFiles(backupsDirectory, "app_backup_*.db", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                    File.Delete(path);
            }
            catch
            {
                // skip files in use or permission issues
            }
        }

        return Task.CompletedTask;
    }
}
