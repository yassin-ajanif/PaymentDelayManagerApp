using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DependencyInjection;
using PaymentDelayApp.Services;
using PaymentDelayApp.ViewModels;
using PaymentDelayApp.Views;
using PaymentDelayApp.Views.Dialogs;
using System.Linq;
using Avalonia.Markup.Xaml;

namespace PaymentDelayApp;

public partial class App : Application
{
    /// <summary>Exit backup budget; cooperative cancellation (exports, prune). SQLite copy is not aborted mid-flight.</summary>
    private static readonly TimeSpan ExitBackupTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Brief pause after showing the backup dialog so the UI is visible before work starts (counts toward <see cref="ExitBackupTimeout"/>).</summary>
    private static readonly TimeSpan ExitBackupDialogSettleDelay = TimeSpan.FromSeconds(1);

    private ServiceProvider? _serviceProvider;
    private bool _exitBackupHandled;
    private bool _exitBackupInProgress;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            services.AddPaymentDelayApp();
            _serviceProvider = services.BuildServiceProvider();

            var db = _serviceProvider.GetRequiredService<PaymentDelayDbContext>();
            DatabaseMigrator.Migrate(db);

            WatcherSettingsFile.EnsureCreated();
            BackupSettingsFile.EnsureCreated();

            var dashboard = _serviceProvider.GetRequiredService<DashboardViewModel>();

            if (HasShowAlertsArgument(desktop))
                dashboard.ShowAlertInvoicesOnly = true;

            var main = new MainWindow { DataContext = dashboard };
            main.Opened += async (_, _) =>
            {
                await dashboard.LoadAsync();
            };
            main.Closing += MainWindow_OnClosing;
            desktop.MainWindow = main;

            desktop.ShutdownRequested += (_, _) =>
            {
                _serviceProvider?.Dispose();
                _serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not MainWindow main)
            return;

        if (_exitBackupHandled)
            return;

        if (_exitBackupInProgress)
        {
            e.Cancel = true;
            return;
        }

        if (!TryGetExitBackupParameters(out var source, out var dir, out var retentionDays))
            return;

        e.Cancel = true;
        _exitBackupInProgress = true;
        _ = RunExitBackupThenCloseAsync(main, source, dir, retentionDays);
    }

    private async Task RunExitBackupThenCloseAsync(
        MainWindow main,
        string source,
        string backupsDirectory,
        int retentionDays)
    {
        BackupProgressWindow? progressWindow = null;
        var allowProgressClose = false;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var w = new BackupProgressWindow();
                progressWindow = w;
                w.Closing += (_, args) =>
                {
                    if (!allowProgressClose)
                        args.Cancel = true;
                };
                w.Show(main);
            });

            using var cts = new CancellationTokenSource(ExitBackupTimeout);
            await Task.Delay(ExitBackupDialogSettleDelay, cts.Token).ConfigureAwait(false);
            var backup = _serviceProvider!.GetRequiredService<IBackupService>();
            await backup.CreateBackupAsync(source, backupsDirectory, retentionDays, cts.Token).ConfigureAwait(false);

            var updated = BackupSettingsFile.LoadOrDefault();
            updated.LastBackupUtc = DateTime.UtcNow;
            BackupSettingsFile.Save(updated);
        }
        catch (OperationCanceledException)
        {
            // Timeout or explicit cancellation.
        }
        catch
        {
            // Best-effort; still close the app.
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                allowProgressClose = true;
                progressWindow?.Close();
                _exitBackupHandled = true;
                _exitBackupInProgress = false;
                main.Close();
            });
        }
    }

    private static bool TryGetExitBackupParameters(
        out string source,
        out string backupsDirectory,
        out int retentionDays)
    {
        source = string.Empty;
        backupsDirectory = string.Empty;
        retentionDays = BackupSettingsFile.DefaultRetentionDays;

        var doc = BackupSettingsFile.LoadOrDefault();
        var s = (doc.DatabasePath ?? string.Empty).Trim();
        var d = (doc.BackupsDirectory ?? string.Empty).Trim();
        if (s.Length == 0 || d.Length == 0)
            return false;

        try
        {
            s = Path.GetFullPath(s);
            d = Path.GetFullPath(d);
        }
        catch
        {
            return false;
        }

        if (!File.Exists(s))
            return false;

        source = s;
        backupsDirectory = d;
        retentionDays = doc.RetentionDays;
        return true;
    }

    private static bool HasShowAlertsArgument(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.Args is { Length: > 0 } args)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, "--show-alerts", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        foreach (var a in Environment.GetCommandLineArgs())
        {
            if (string.Equals(a, "--show-alerts", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}