namespace AlterWatcherService;

/// <summary>
/// Appends timestamped lines to <c>errors.txt</c> next to the executable (infos, warnings, and errors).
/// </summary>
internal static class ErrorsTextFile
{
    private static readonly object Sync = new();

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "errors.txt");

    internal static void AppendLine(string message) => AppendRaw(message);

    internal static void AppendInfo(string message) => AppendRaw($"[INFO] {message}");

    internal static void AppendWarning(string message) => AppendRaw($"[WARN] {message}");

    internal static void AppendException(Exception ex, string? context = null)
    {
        if (!string.IsNullOrEmpty(context))
            AppendRaw($"[ERROR] {context}");
        AppendRaw($"[ERROR] {ex}");
    }

    private static void AppendRaw(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(FilePath, line);
            }
        }
        catch
        {
            // Do not throw from diagnostics.
        }
    }
}
