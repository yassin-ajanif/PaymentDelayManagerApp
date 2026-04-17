using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PaymentDelayApp.DataAccessLayer;

namespace AlterWatcherService;

internal static class PaymentDelayAppLauncher
{
    private const string ShowAlertsArg = "--show-alerts";

    /// <summary>Runs <c>schtasks.exe /Run /TN ...</c> so Task Scheduler launches the GUI in the user desktop session.</summary>
    internal static bool TryLaunchViaScheduledTask(string taskName, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            const string msg = "scheduledTaskName is empty; cannot run schtasks /Run.";
            logger.LogWarning(msg);
            ErrorsTextFile.AppendWarning(msg);
            return false;
        }

        var trimmed = taskName.Trim();
        try
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var schtasks = Path.Combine(systemRoot, "schtasks.exe");
            ErrorsTextFile.AppendInfo($"Attempting schtasks: {schtasks} /Run /TN \"{trimmed}\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = schtasks,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("/Run");
            startInfo.ArgumentList.Add("/TN");
            startInfo.ArgumentList.Add(trimmed);

            using var p = Process.Start(startInfo);

            if (p is null)
            {
                const string msg = "schtasks.exe Process.Start returned null.";
                logger.LogWarning(msg);
                ErrorsTextFile.AppendWarning(msg);
                return false;
            }

            p.WaitForExit();
            var exit = p.ExitCode;
            var stderr = p.StandardError.ReadToEnd();
            var stdout = p.StandardOutput.ReadToEnd();
            if (exit == 0)
            {
                logger.LogInformation("schtasks /Run succeeded for task {Task}.", trimmed);
                ErrorsTextFile.AppendInfo($"schtasks /Run succeeded for task \"{trimmed}\".");
                return true;
            }

            var detail = string.Join(" ", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
            logger.LogWarning("schtasks /Run failed for task {Task}. ExitCode={Exit}. Output: {Output}", trimmed, exit, detail);
            ErrorsTextFile.AppendWarning($"schtasks /Run failed for task \"{trimmed}\". ExitCode={exit}. {detail}");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run schtasks for task {Task}", trimmed);
            ErrorsTextFile.AppendException(ex, $"Failed to run schtasks for task \"{trimmed}\"");
            return false;
        }
    }

    /// <summary>Resolves PaymentDelayApp.exe: JSON path, then side-by-side publish, then common dev output locations.</summary>
    internal static string? ResolveExePath(WatcherSettingsDocument settings, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(settings.PaymentDelayAppExePath))
        {
            var configured = settings.PaymentDelayAppExePath.Trim();
            if (File.Exists(configured))
            {
                var resolved = Path.GetFullPath(configured);
                ErrorsTextFile.AppendInfo($"Resolved PaymentDelayApp.exe from watcher settings: {resolved}");
                return resolved;
            }

            logger.LogWarning("paymentDelayAppExePath is set but file not found: {Path}", configured);
            ErrorsTextFile.AppendWarning($"paymentDelayAppExePath is set but file not found: {configured}");
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
            {
                ErrorsTextFile.AppendInfo($"Resolved PaymentDelayApp.exe from candidate path: {c}");
                return c;
            }
        }

        ErrorsTextFile.AppendWarning("Could not resolve PaymentDelayApp.exe (no settings path and no candidate file exists).");
        return null;
    }

    internal static bool TryLaunchShowAlerts(string? exePath, ILogger logger)
    {
        if (string.IsNullOrEmpty(exePath))
        {
            const string msg =
                "PaymentDelayApp.exe not found. Set paymentDelayAppExePath in watcher-settings.json or publish the GUI next to AlterWatcherService.";
            logger.LogWarning(msg);
            ErrorsTextFile.AppendWarning(msg);
            return false;
        }

        try
        {
            ErrorsTextFile.AppendInfo($"Attempting to start PaymentDelayApp: {exePath} (args: {ShowAlertsArg})");
            var workDir = Path.GetDirectoryName(exePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = ShowAlertsArg,
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrEmpty(workDir) ? AppContext.BaseDirectory : workDir,
            });
            logger.LogInformation("Started PaymentDelayApp with {Arg}.", ShowAlertsArg);
            ErrorsTextFile.AppendInfo($"Started PaymentDelayApp with {ShowAlertsArg}.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start PaymentDelayApp at {Path}", exePath);
            ErrorsTextFile.AppendException(ex, $"Failed to start PaymentDelayApp at {exePath}");
            return false;
        }
    }
}
