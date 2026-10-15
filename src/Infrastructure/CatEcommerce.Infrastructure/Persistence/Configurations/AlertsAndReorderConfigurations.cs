using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.13 / §12.1 / ADR-110 -- <c>inventory.stock_alert</c>. The dedup key is unique where present (an expiry threshold is never raised twice),
/// and there is at most one live <c>LowStock</c> or <c>LedgerMismatch</c> alert per (SKU, location) -- both predicates are built from the enumerations.</summary>
public sealed class InventoryAlertConfiguration : IEntityTypeConfiguration<InventoryAlertAggregate>
{
    public void Configure(EntityTypeBuilder<InventoryAlertAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_alert");

        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.ThresholdDays).HasColumnName("threshold_days");
        builder.Property(x => x.QuantityAtRaise).HasColumnName("quantity_at_raise").HasPrecision(12, 3);
        builder.Property(x => x.ReferenceKind).HasNullableEnumeration("reference_kind");
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id");
        builder.Property(x => x.DedupKey).HasColumnName("dedup_key").HasMaxLength(200);
        builder.Property(x => x.RaisedOn).HasColumnName("raised_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by");
        builder.Property(x => x.AcknowledgedOn).HasColumnName("acknowledged_on").HasColumnType("timestamptz");
        builder.Property(x => x.ResolvedOn).HasColumnName("resolved_on").HasColumnType("timestamptz");
        builder.Property(x => x.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(InventoryRules.NoteMaxLength);

        builder.HasIndex(x => x.DedupKey).IsUnique().HasDatabaseName("ix_stock_alert_dedup_key").HasFilter("dedup_key IS NOT NULL");
        builder.HasIndex(x => new { x.Kind, x.SkuId, x.LocationId }).IsUnique().HasDatabaseName("ix_stock_alert_one_live_per_pair")
            .HasFilter($"status <> {InventoryAlertStatus.Resolved.Value} AND kind IN ({StockAlertKind.LowStock.Value}, {StockAlertKind.LedgerMismatch.Value})");
        builder.HasIndex(x => new { x.BranchId, x.Status, x.Kind }).HasDatabaseName("ix_stock_alert_branch_status_kind");
    }
}

/// <summary>HO-IMP-001 §8.13 / §12.1 (US-IMP-12) -- one policy per (SKU, location); <c>ReorderPoint = 0</c> silences the pair.</summary>
public sealed class ReorderPolicyConfiguration : IEntityTypeConfiguration<ReorderPolicyAggregate>
{
    public void Configure(EntityTypeBuilder<ReorderPolicyAggregate> builder)
    {
        builder.ConfigureInventoryRoot("reorder_policy");

        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.ReorderPoint).HasColumnName("reorder_point").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.ReorderQuantity).HasColumnName("reorder_quantity").HasPrecision(12, 3);
        builder.Property(x => x.PreferredSupplierId).HasColumnName("preferred_supplier_id");

        builder.HasIndex(x => new { x.SkuId, x.LocationId }).IsUnique().HasDatabaseName("ix_reorder_policy_sku_location");
        builder.HasIndex(x => x.LocationId).HasDatabaseName("ix_reorder_policy_location");
    }
}
