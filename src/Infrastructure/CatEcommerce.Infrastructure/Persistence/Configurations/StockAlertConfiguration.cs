using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1. The filtered unique index is the "one Pending alert per
/// customer per SKU" rule; its predicate is built from the enumeration (U-X5).</summary>
public sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlertAggregate>
{
    public void Configure(EntityTypeBuilder<StockAlertAggregate> builder)
    {
        builder.ToTable("stock_alert", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(v => v.Value, v => Enumeration.FromValue<StockAlertStatus>(v)).IsRequired();
        builder.Property(x => x.ClosureReason).HasColumnName("closure_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<StockAlertClosureReason>(v.Value));
        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.NotifiedOn).HasColumnName("notified_on").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.CustomerId, x.SkuId }).IsUnique().HasDatabaseName("ix_stock_alert_pending_per_customer_sku")
            .HasFilter($"status = {StockAlertStatus.Pending.Value}");
        builder.HasIndex(x => new { x.Status, x.SkuId }).HasDatabaseName("ix_stock_alert_status_sku");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
