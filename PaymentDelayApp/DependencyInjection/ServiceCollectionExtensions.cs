using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Services;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DataAccessLayer.Access;
using PaymentDelayApp.Services;
using PaymentDelayApp.ViewModels;

namespace PaymentDelayApp.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentDelayApp(this IServiceCollection services)
    {
        services.AddDbContext<PaymentDelayDbContext>(
            options => options.UseSqlite(PaymentDelayDbPaths.BuildConnectionString()),
            ServiceLifetime.Singleton,
            ServiceLifetime.Singleton);

        services.AddSingleton<IInvoiceAccess, InvoiceAccess>();
        services.AddSingleton<ISupplierAccess, SupplierAccess>();
        services.AddSingleton<IInvoiceService, InvoiceService>();
        services.AddSingleton<ISupplierService, SupplierService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IInvoiceDashboardExportService, InvoiceDashboardExportService>();
        services.AddSingleton<DashboardViewModel>();

        return services;
    }
}
