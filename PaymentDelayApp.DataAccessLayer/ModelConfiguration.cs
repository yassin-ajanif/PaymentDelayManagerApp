using Microsoft.EntityFrameworkCore;
using PaymentDelayApp.BusinessLayer.Models;

namespace PaymentDelayApp.DataAccessLayer;

public static class ModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Ice).HasMaxLength(100);
            entity.Property(e => e.FiscalId).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(1000);
            entity.Property(e => e.Activite).HasMaxLength(500);
            entity.Property(e => e.AlertSeuilJours).IsRequired();
            entity
                .HasMany(e => e.Invoices)
                .WithOne(e => e.Supplier!)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceDate).IsRequired();
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Designation).HasMaxLength(2000);
            entity.Property(e => e.TtcAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.IsSettled).IsRequired();
            entity.Property(e => e.IsPaymentAlert).IsRequired();
            entity.HasIndex(e => e.SupplierId).HasDatabaseName("IX_Invoices_SupplierId");
            entity.HasIndex(e => e.InvoiceDate).HasDatabaseName("IX_Invoices_InvoiceDate");
            entity.HasIndex(e => e.IsSettled).HasDatabaseName("IX_Invoices_IsSettled");
            entity
                .HasIndex(e => new { e.IsPaymentAlert, e.IsSettled })
                .HasDatabaseName("IX_Invoices_IsPaymentAlert_IsSettled");
        });
    }
}
