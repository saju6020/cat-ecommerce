using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-IMP-001 §8.7 / §12.1 / ADR-098, ADR-100. A reservation is an ENTRY (<c>Reserve</c> +, <c>Release</c> -), append-only like the ledger:
/// <c>tg_stock_reservation_immutable</c> refuses UPDATE, DELETE and TRUNCATE, and the REMAINING reservation of a reference is derived (§8.7), never
/// stored. <c>sequence</c> is a database identity EF never writes.
/// </summary>
public sealed class StockReservationEntryConfiguration : IEntityTypeConfiguration<StockReservationEntryAggregate>
{
    public void Configure(EntityTypeBuilder<StockReservationEntryAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_reservation_entry");

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
        builder.Property(x => x.ReferenceKind).HasEnumeration("reference_kind");
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(x => x.ReferenceLineId).HasColumnName("reference_line_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.CatId).HasColumnName("cat_id");
        builder.Property(x => x.FefoOverridden).HasColumnName("fefo_overridden").IsRequired();
        builder.Property(x => x.OverrideReason).HasColumnName("override_reason").HasMaxLength(InventoryRules.NoteMaxLength);
        builder.Property(x => x.PerformedBy).HasColumnName("performed_by").IsRequired();

        builder.HasOne<BatchAggregate>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocationAggregate>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ReferenceKind, x.ReferenceId, x.ReferenceLineId }).HasDatabaseName("ix_stock_reservation_entry_reference");
        builder.HasIndex(x => x.BatchId).HasDatabaseName("ix_stock_reservation_entry_batch");
    }
}
