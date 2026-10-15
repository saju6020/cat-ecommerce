using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.10 / §12.1 -- a posted transfer is immutable; its two ledger entries per line carry its number as their reference.</summary>
public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransferAggregate>
{
    public void Configure(EntityTypeBuilder<StockTransferAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_transfer");

        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_stock_transfer_number");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.FromLocationId).HasColumnName("from_location_id").IsRequired();
        builder.Property(x => x.ToLocationId).HasColumnName("to_location_id").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(InventoryRules.NoteMaxLength);
        builder.Property(x => x.TransferredOn).HasColumnName("transferred_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.TransferredBy).HasColumnName("transferred_by").IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.TransferredOn }).HasDatabaseName("ix_stock_transfer_branch_transferred_on");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("stock_transfer_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("stock_transfer_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();
            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.BatchId).HasColumnName("batch_id").IsRequired();
            line.Property(l => l.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();
            line.HasIndex(l => l.BatchId).HasDatabaseName("ix_stock_transfer_line_batch");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>HO-IMP-001 §8.10 / §12.1 / ADR-107 -- a reason CATEGORY and a mandatory note; <c>value_base</c> is money (<c>numeric(12,2)</c>), a line's unit
/// cost a four-place <c>UnitCost</c>.</summary>
public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustmentAggregate>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_adjustment");

        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_stock_adjustment_number");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.Reason).HasEnumeration("reason");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(InventoryRules.NoteMaxLength).IsRequired();
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.ValueBase).HasColumnName("value_base").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
        builder.Property(x => x.DecidedOn).HasColumnName("decided_on").HasColumnType("timestamptz");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(InventoryRules.NoteMaxLength);
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_stock_adjustment_branch_status");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("stock_adjustment_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("stock_adjustment_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();
            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.BatchId).HasColumnName("batch_id").IsRequired();
            line.Property(l => l.QuantityDelta).HasColumnName("quantity_delta").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.UnitCostBase).HasColumnName("unit_cost_base").HasPrecision(14, 4).IsRequired();
            line.HasIndex(l => l.BatchId).HasDatabaseName("ix_stock_adjustment_line_batch");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>HO-IMP-001 §8.10 / §12.1 / ADR-108. <c>ix_stock_count_one_active</c> is "one Open, Submitted or AwaitingApproval count per location" -- the
/// predicate is built from the enumeration (U-X2). A line's book quantity and variance are written once, at submit, and frozen.</summary>
public sealed class StockCountConfiguration : IEntityTypeConfiguration<StockCountAggregate>
{
    public void Configure(EntityTypeBuilder<StockCountAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_count");

        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_stock_count_number");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.Scope).HasEnumeration("scope");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.VarianceValueBase).HasColumnName("variance_value_base").HasPrecision(12, 2);
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.OpenedOn).HasColumnName("opened_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.OpenedBy).HasColumnName("opened_by").IsRequired();
        builder.Property(x => x.SubmittedOn).HasColumnName("submitted_on").HasColumnType("timestamptz");
        builder.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
        builder.Property(x => x.CommitRequestedBy).HasColumnName("commit_requested_by");
        builder.Property(x => x.CommittedOn).HasColumnName("committed_on").HasColumnType("timestamptz");
        builder.Property(x => x.DecidedBy).HasColumnName("decided_by");

        var idList = new ValueComparer<IReadOnlyList<Guid>>(
            (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)), v => v.ToList());
        builder.Property(x => x.SkuIds).HasColumnName("sku_ids").HasColumnType("uuid[]")
            .HasConversion(v => v.ToArray(), v => v.ToList())
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .Metadata.SetValueComparer(idList);

        builder.HasIndex(x => x.LocationId).IsUnique().HasDatabaseName("ix_stock_count_one_active")
            .HasFilter($"status IN ({StockCountStatus.Open.Value}, {StockCountStatus.Submitted.Value}, {StockCountStatus.AwaitingApproval.Value})");
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_stock_count_branch_status");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("stock_count_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("stock_count_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();
            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.BatchId).HasColumnName("batch_id").IsRequired();
            line.Property(l => l.CountedQuantity).HasColumnName("counted_quantity").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.BookQuantity).HasColumnName("book_quantity").HasPrecision(12, 3);
            line.Property(l => l.Variance).HasColumnName("variance").HasPrecision(12, 3);
            line.Property(l => l.UnitCostBase).HasColumnName("unit_cost_base").HasPrecision(14, 4).IsRequired();
            line.Property(l => l.CountedBy).HasColumnName("counted_by").IsRequired();
            line.Property(l => l.CountedOn).HasColumnName("counted_on").HasColumnType("timestamptz").IsRequired();
            line.HasIndex("stock_count_id", nameof(Domain.Entities.StockCountLine.SkuId), nameof(Domain.Entities.StockCountLine.BatchId)).IsUnique().HasDatabaseName("ix_stock_count_line_count_sku_batch");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
