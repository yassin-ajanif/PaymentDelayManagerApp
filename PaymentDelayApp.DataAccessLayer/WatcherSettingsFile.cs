using System.Text.Json;

namespace PaymentDelayApp.DataAccessLayer;

public static class WatcherSettingsFile
{
    private static readonly object DiagnosticsSync = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public const int DefaultScanIntervalMinutes = 1;
    private const int MinScanIntervalMinutes = 1;
    private const int MaxScanIntervalMinutes = 24 * 60;

    public static int ClampScanInterval(int minutes) =>
        Math.Clamp(minutes, MinScanIntervalMinutes, MaxScanIntervalMinutes);

    public static WatcherSettingsDocument LoadOrDefault()
    {
        var path = PaymentDelayDbPaths.WatcherSettingsFilePath;
        try
        {
            if (!File.Exists(path))
            {
                AppendWatcherSettingsDiagnostics(
                    $"Watcher-settings file was not found at location \"{path}\". Using default settings.");
                return DefaultDocument();
            }

            AppendWatcherSettingsDiagnostics($"Watcher-settings file found at location \"{path}\".");

            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<WatcherSettingsDocument>(json, JsonOptions);
            if (doc is null)
            {
                AppendWatcherSettingsDiagnostics(
                    $"Watcher-settings file found at location \"{path}\" but could not be deserialized. Using default settings.");
                return DefaultDocument();
            }

            doc.ScanIntervalMinutes = ClampScanInterval(doc.ScanIntervalMinutes);
            return doc;
        }
        catch (Exception ex)
        {
            AppendWatcherSettingsDiagnostics(
                $"Watcher-settings load failed at location \"{path}\": {ex.Message}. Using default settings.");
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

    /// <summary>Creates <c>watcher-settings.json</c> with defaults if it does not exist (idempotent).</summary>
    public static void EnsureCreated()
    {
        var path = PaymentDelayDbPaths.WatcherSettingsFilePath;
        if (File.Exists(path))
        {
            AppendWatcherSettingsDiagnostics($"Watcher-settings already exists at \"{path}\". No action.");
            return;
        }

        try
        {
            Save(DefaultDocument());
            AppendWatcherSettingsDiagnostics($"Watcher-settings created with defaults at \"{path}\".");
        }
        catch (Exception ex)
        {
            AppendWatcherSettingsDiagnostics($"Failed to create watcher-settings at \"{path}\": {ex.Message}");
        }
    }

    private static WatcherSettingsDocument DefaultDocument() => new()
    {
        SchemaVersion = 1,
        ScanIntervalMinutes = DefaultScanIntervalMinutes,
        PaymentDelayAppExePath = null,
    };

    /// <summary>
    /// Appends to <c>errors.txt</c> next to the running assembly (same layout as AlterWatcherService diagnostics).
    /// </summary>
    private static void AppendWatcherSettingsDiagnostics(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}{Environment.NewLine}";
            var logPath = Path.Combine(AppContext.BaseDirectory, "errors.txt");
            lock (DiagnosticsSync)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Do not throw from diagnostics.
        }
    }
}
