using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.DataAccessLayer;

namespace AlterWatcherService;

/// <summary>
/// Polls the invoice database on an interval read from <see cref="WatcherSettingsFile"/>.
/// </summary>
/// <remarks>
/// Paths use the process identity’s LocalAppData (see <see cref="PaymentDelayDbPaths"/>). A Windows Service
/// running as LocalSystem uses a different profile than the desktop user; align the service account with
/// the app user or use a shared CommonApplicationData layout if both must see the same files.
/// </remarks>
public sealed class WatcherScanHostedService : BackgroundService
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<WatcherScanHostedService> _logger;

    public WatcherScanHostedService(IInvoiceService invoiceService, ILogger<WatcherScanHostedService> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AlterWatcherService started. Database={Db}; WatcherSettings={Settings}",
            PaymentDelayDbPaths.DatabaseFilePath,
            PaymentDelayDbPaths.WatcherSettingsFilePath);
        ErrorsTextFile.AppendInfo(
            $"AlterWatcherService started. Database={PaymentDelayDbPaths.DatabaseFilePath}; WatcherSettings={PaymentDelayDbPaths.WatcherSettingsFilePath}");
        _logger.LogInformation(
            "If this service runs as LocalSystem, LocalAppData is not the interactive user's folder; use the same Windows account as PaymentDelayApp or a shared data directory.");
        ErrorsTextFile.AppendInfo(
            "If this service runs as LocalSystem, LocalAppData is not the interactive user's folder; use the same Windows account as PaymentDelayApp or a shared data directory.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = WatcherSettingsFile.LoadOrDefault();
            var minutes = settings.ScanIntervalMinutes;

            try
            {
                await _invoiceService.RefreshPaymentAlertFlagsAsync(stoppingToken);
                var alertUnsettled = await _invoiceService.CountUnsettledPaymentAlertsAsync(stoppingToken);
                _logger.LogInformation(
                    "Scan finished. Factures en alerte non réglées: {Count}. Prochaine attente: {Minutes} min.",
                    alertUnsettled,
                    minutes);
                ErrorsTextFile.AppendInfo(
                    $"Scan finished. Factures en alerte non réglées: {alertUnsettled}. Prochaine attente: {minutes} min.");

                if (alertUnsettled > 0)
                {
                    var taskName = string.IsNullOrWhiteSpace(settings.ScheduledTaskName)
                        ? "PaymentDelayAppShowAlerts"
                        : settings.ScheduledTaskName;
                    ErrorsTextFile.AppendInfo(
                        $"Unsettled alerts ({alertUnsettled}) > 0; triggering scheduled task \"{taskName}\" (schtasks /Run).");
                    PaymentDelayAppLauncher.TryLaunchViaScheduledTask(taskName, _logger);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scan failed.");
                ErrorsTextFile.AppendException(ex, "Scan failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
