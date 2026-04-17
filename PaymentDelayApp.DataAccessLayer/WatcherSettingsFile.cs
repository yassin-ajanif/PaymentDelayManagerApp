using System.Text.Json;

namespace PaymentDelayApp.DataAccessLayer;

public static class WatcherSettingsFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public const int DefaultScanIntervalMinutes = 5;
    private const int MinScanIntervalMinutes = 1;
    private const int MaxScanIntervalMinutes = 24 * 60;

    public static int ClampScanInterval(int minutes) =>
        Math.Clamp(minutes, MinScanIntervalMinutes, MaxScanIntervalMinutes);

    public static WatcherSettingsDocument LoadOrDefault()
    {
        try
        {
            var path = PaymentDelayDbPaths.WatcherSettingsFilePath;
            if (!File.Exists(path))
                return DefaultDocument();

            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<WatcherSettingsDocument>(json, JsonOptions);
            if (doc is null)
                return DefaultDocument();
            doc.ScanIntervalMinutes = ClampScanInterval(doc.ScanIntervalMinutes);
            return doc;
        }
        catch
        {
            return DefaultDocument();
        }
    }

    public static void Save(WatcherSettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.ScanIntervalMinutes = ClampScanInterval(document.ScanIntervalMinutes);
        var path = PaymentDelayDbPaths.WatcherSettingsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static WatcherSettingsDocument DefaultDocument() => new()
    {
        SchemaVersion = 1,
        ScanIntervalMinutes = DefaultScanIntervalMinutes,
        PaymentDelayAppExePath = null,
    };
}
