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
            var dialogs = _serviceProvider.GetRequiredService<IDialogService>();

            var main = new MainWindow { DataContext = dashboard };
            main.Opened += async (_, _) =>
            {
                await dashboard.LoadAsync();
                await dialogs.ShowStartupPaymentAlertsIfNeededAsync();
            };
            desktop.MainWindow = main;

            desktop.ShutdownRequested += (_, _) =>
            {
                _serviceProvider?.Dispose();
                _serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
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