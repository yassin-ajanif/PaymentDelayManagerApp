namespace PaymentDelayApp.DataAccessLayer;

public sealed class WatcherSettingsDocument
{
    public int SchemaVersion { get; set; } = 1;
    public int ScanIntervalMinutes { get; set; } = 5;
    public string? PaymentDelayAppExePath { get; set; }

    /// <summary>
    /// Windows Task Scheduler task name (e.g. <c>PaymentDelayAppShowAlerts</c>) to run via <c>schtasks /Run</c>
    /// so the GUI starts in the interactive user session instead of Session 0.
    /// </summary>
    public string? ScheduledTaskName { get; set; }
}
