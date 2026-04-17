namespace PaymentDelayApp.DataAccessLayer;

public sealed class BackupSettingsDocument
{
    public int SchemaVersion { get; set; }
    public string? DatabasePath { get; set; }
    public string? BackupsDirectory { get; set; }
    public int RetentionDays { get; set; }
    public DateTime? LastBackupUtc { get; set; }
}
