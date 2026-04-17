using System.Text.Json;

namespace PaymentDelayApp.DataAccessLayer;

public static class BackupSettingsFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public const int DefaultRetentionDays = 30;
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 3650;

    public static int ClampRetentionDays(int days) =>
        Math.Clamp(days, MinRetentionDays, MaxRetentionDays);

    public static BackupSettingsDocument LoadOrDefault()
    {
        try
        {
            var path = PaymentDelayDbPaths.BackupSettingsFilePath;
            if (!File.Exists(path))
                return DefaultDocument();

            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<BackupSettingsDocument>(json, JsonOptions);
            return doc ?? DefaultDocument();
        }
        catch
        {
            return DefaultDocument();
        }
    }

    public static void Save(BackupSettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.RetentionDays = ClampRetentionDays(document.RetentionDays);
        var path = PaymentDelayDbPaths.BackupSettingsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static BackupSettingsDocument DefaultDocument() => new()
    {
        SchemaVersion = 1,
        DatabasePath = PaymentDelayDbPaths.DatabaseFilePath,
        BackupsDirectory = PaymentDelayDbPaths.BackupsDirectory,
        RetentionDays = DefaultRetentionDays,
        LastBackupUtc = null,
    };
}
