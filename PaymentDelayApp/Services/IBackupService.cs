namespace PaymentDelayApp.Services;

/// <summary>SQLite online backup and retention pruning for <c>app_backup_*.db</c> files.</summary>
public interface IBackupService
{
    /// <summary>Creates <c>app_backup_yyyyMMdd_HHmmss.db</c> under <paramref name="backupsDirectory"/> from <paramref name="sourceDatabasePath"/>, then prunes old backups.</summary>
    Task CreateBackupAsync(
        string sourceDatabasePath,
        string backupsDirectory,
        int retentionDays,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes <c>app_backup_*.db</c> in <paramref name="backupsDirectory"/> older than <paramref name="retentionDays"/> by last write time (UTC).</summary>
    Task PruneBackupsAsync(
        string backupsDirectory,
        int retentionDays,
        CancellationToken cancellationToken = default);
}
