namespace PaymentDelayApp.DataAccessLayer;

public static class PaymentDelayDbPaths
{
    public static string AppDataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaymentDelayApp");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabaseFilePath => Path.Combine(AppDataDirectory, "app.db");

    public static string BackupsDirectory
    {
        get
        {
            var dir = Path.Combine(AppDataDirectory, "backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string BackupSettingsFilePath => Path.Combine(AppDataDirectory, "backup-settings.json");

    public static string WatcherSettingsFilePath => Path.Combine(AppDataDirectory, "watcher-settings.json");

    public static string BuildConnectionString() => $"Data Source={DatabaseFilePath}";
}
