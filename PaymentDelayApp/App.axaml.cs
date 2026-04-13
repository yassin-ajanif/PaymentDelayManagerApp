using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DependencyInjection;
using PaymentDelayApp.Services;
using PaymentDelayApp.ViewModels;
using PaymentDelayApp.Views;
using System.Linq;
using Avalonia.Markup.Xaml;

namespace PaymentDelayApp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

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

            var dashboard = _serviceProvider.GetRequiredService<DashboardViewModel>();

            if (HasShowAlertsArgument(desktop))
                dashboard.ShowAlertInvoicesOnly = true;

            var main = new MainWindow { DataContext = dashboard };
            main.Opened += async (_, _) =>
            {
                await dashboard.LoadAsync();
            };
            desktop.MainWindow = main;

            desktop.ShutdownRequested += (_, _) =>
            {
                try
                {
                    TryBackupOnExit(_serviceProvider);
                }
                finally
                {
                    _serviceProvider?.Dispose();
                    _serviceProvider = null;
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Writes a timestamped SQLite backup using <see cref="BackupSettingsFile"/> before the host disposes services.
    /// Disposes <see cref="PaymentDelayDbContext"/> first so the main connection does not block the backup connection.
    /// </summary>
    private static void TryBackupOnExit(ServiceProvider? serviceProvider)
    {
        if (serviceProvider is null)
            return;

        try
        {
            var dbContext = serviceProvider.GetService<PaymentDelayDbContext>();
            dbContext?.Dispose();

            var backup = serviceProvider.GetRequiredService<IBackupService>();
            var doc = BackupSettingsFile.LoadOrDefault();
            var source = (doc.DatabasePath ?? string.Empty).Trim();
            var dir = (doc.BackupsDirectory ?? string.Empty).Trim();
            if (source.Length == 0 || dir.Length == 0)
                return;

            try
            {
                source = Path.GetFullPath(source);
                dir = Path.GetFullPath(dir);
            }
            catch
            {
                return;
            }

            if (!File.Exists(source))
                return;

            backup.CreateBackupAsync(source, dir, doc.RetentionDays, CancellationToken.None).GetAwaiter().GetResult();

            var updated = BackupSettingsFile.LoadOrDefault();
            updated.LastBackupUtc = DateTime.UtcNow;
            BackupSettingsFile.Save(updated);
        }
        catch
        {
            // Best-effort on exit; avoid blocking or surfacing UI.
        }
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