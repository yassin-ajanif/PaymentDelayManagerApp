using AlterWatcherService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentDelayApp.BusinessLayer.Abstractions;
using PaymentDelayApp.BusinessLayer.Services;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.DataAccessLayer.Access;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();

builder.Services.AddDbContext<PaymentDelayDbContext>(
    options => options.UseSqlite(PaymentDelayDbPaths.BuildConnectionString()),
    ServiceLifetime.Singleton,
    ServiceLifetime.Singleton);

builder.Services.AddSingleton<IInvoiceAccess, InvoiceAccess>();
builder.Services.AddSingleton<ISupplierAccess, SupplierAccess>();
builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
builder.Services.AddHostedService<WatcherScanHostedService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDelayDbContext>();
    DatabaseMigrator.Migrate(db);
    ErrorsTextFile.AppendInfo("Database migration completed.");

    WatcherSettingsFile.EnsureCreated();
    BackupSettingsFile.EnsureCreated();
    ErrorsTextFile.AppendInfo("Settings files ensured (watcher-settings.json, backup-settings.json).");
}

await host.RunAsync();
