using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-IMP-001 §8.7 / §12.1 / ADR-098. One row per movement, append-only: the database refuses <c>UPDATE</c>, <c>DELETE</c> and <c>TRUNCATE</c>
/// (<c>tg_stock_ledger_immutable</c>), and the application has no mutation method. <c>sequence</c> is a <c>bigint GENERATED ALWAYS AS IDENTITY</c>
/// that EF reads and never writes. Foreign keys go only to the batch and the location (ADR-011); the SKU, branch, customer and cat are
/// references by id (rule 2). The consuming-kind index set serves the recall trace (ADR-104).
/// </summary>
public sealed class StockLedgerEntryConfiguration : IEntityTypeConfiguration<StockLedgerEntryAggregate>
{
    public void Configure(EntityTypeBuilder<StockLedgerEntryAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_ledger_entry");

        builder.Property(x => x.Sequence).HasColumnName("sequence").ValueGeneratedOnAdd().UseIdentityAlwaysColumn();
        builder.Property(x => x.Sequence).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(x => x.Sequence).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.QuantityDelta).HasColumnName("quantity_delta").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.ConsumesReservation).HasColumnName("consumes_reservation").IsRequired();
        builder.Property(x => x.ReferenceKind).HasEnumeration("reference_kind");
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(x => x.ReferenceLineId).HasColumnName("reference_line_id");
        builder.Property(x => x.ReferenceLabel).HasColumnName("reference_label").HasMaxLength(60);
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.CatId).HasColumnName("cat_id");
        builder.Property(x => x.FefoOverridden).HasColumnName("fefo_overridden").IsRequired();
        builder.Property(x => x.OverrideReason).HasColumnName("override_reason").HasMaxLength(InventoryRules.NoteMaxLength);
        builder.Property(x => x.PerformedBy).HasColumnName("performed_by").IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(InventoryRules.NoteMaxLength);

        builder.HasOne<BatchAggregate>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocationAggregate>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        // The trace, consumption and reference lookups, the date-ranged ledger search and the recall stamp (§12.1). (batch_id, sequence)
        // doubles as the batch's own history.
        builder.HasIndex(x => new { x.BatchId, x.Sequence }).HasDatabaseName("ix_stock_ledger_entry_batch_sequence");
        builder.HasIndex(x => new { x.SkuId, x.LocationId, x.OccurredOn }).HasDatabaseName("ix_stock_ledger_entry_sku_location_occurred_on");
        builder.HasIndex(x => new { x.ReferenceKind, x.ReferenceId }).HasDatabaseName("ix_stock_ledger_entry_reference");
        builder.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_stock_ledger_entry_occurred_on");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_stock_ledger_entry_customer").HasFilter("customer_id IS NOT NULL");
        builder.HasIndex(x => x.CatId).HasDatabaseName("ix_stock_ledger_entry_cat").HasFilter("cat_id IS NOT NULL");
        builder.HasIndex(x => new { x.BranchId, x.Kind, x.OccurredOn }).HasDatabaseName("ix_stock_ledger_entry_branch_kind_occurred_on");
    }
}
