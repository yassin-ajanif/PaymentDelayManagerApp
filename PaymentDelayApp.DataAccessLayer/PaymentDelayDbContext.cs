using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.DataAccessLayer;

public class PaymentDelayDbContext : DbContext
{
    public PaymentDelayDbContext(DbContextOptions<PaymentDelayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        ModelConfiguration.Apply(modelBuilder);
}
