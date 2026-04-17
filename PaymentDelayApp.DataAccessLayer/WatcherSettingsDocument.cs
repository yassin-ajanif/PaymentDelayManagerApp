namespace PaymentDelayApp.DataAccessLayer;

public sealed class WatcherSettingsDocument
{
    public int SchemaVersion { get; set; } = 1;
    public int ScanIntervalMinutes { get; set; } = 5;
    public string? PaymentDelayAppExePath { get; set; }
}
