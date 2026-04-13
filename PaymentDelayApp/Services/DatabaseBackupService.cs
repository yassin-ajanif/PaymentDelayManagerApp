using Microsoft.Data.Sqlite;
using PaymentDelayApp.DataAccessLayer;

namespace PaymentDelayApp.Services;

public sealed class DatabaseBackupService : IBackupService
{
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

        var destName = $"app_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
        var destPath = Path.Combine(backupsDirectory, destName);
        if (File.Exists(destPath))
            destPath = Path.Combine(backupsDirectory, $"app_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db");

        var sourceCs = new SqliteConnectionStringBuilder { DataSource = sourceDatabasePath }.ToString();
        var destCs = new SqliteConnectionStringBuilder { DataSource = destPath }.ToString();

        await using var source = new SqliteConnection(sourceCs);
        await source.OpenAsync(cancellationToken);
        await using var dest = new SqliteConnection(destCs);
        await dest.OpenAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        source.BackupDatabase(dest);

        await PruneBackupsAsync(backupsDirectory, retentionDays, cancellationToken);
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
