using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PaymentDelayApp.DataAccessLayer;

namespace AlterWatcherService;

internal static class PaymentDelayAppLauncher
{
    private const string ShowAlertsArg = "--show-alerts";

    /// <summary>Resolves PaymentDelayApp.exe: JSON path, then side-by-side publish, then common dev output locations.</summary>
    internal static string? ResolveExePath(WatcherSettingsDocument settings, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(settings.PaymentDelayAppExePath))
        {
            var configured = settings.PaymentDelayAppExePath.Trim();
            if (File.Exists(configured))
                return Path.GetFullPath(configured);
            logger.LogWarning("paymentDelayAppExePath is set but file not found: {Path}", configured);
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "PaymentDelayApp.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PaymentDelayApp", "bin", "Debug", "net9.0", "PaymentDelayApp.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PaymentDelayApp", "bin", "Release", "net9.0", "PaymentDelayApp.exe")),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    internal static bool TryLaunchShowAlerts(string? exePath, ILogger logger)
    {
        if (string.IsNullOrEmpty(exePath))
        {
            logger.LogWarning(
                "PaymentDelayApp.exe not found. Set paymentDelayAppExePath in watcher-settings.json or publish the GUI next to AlterWatcherService.");
            return false;
        }

        try
        {
            var workDir = Path.GetDirectoryName(exePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = ShowAlertsArg,
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrEmpty(workDir) ? AppContext.BaseDirectory : workDir,
            });
            logger.LogInformation("Started PaymentDelayApp with {Arg}.", ShowAlertsArg);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start PaymentDelayApp at {Path}", exePath);
            return false;
        }
    }
}
