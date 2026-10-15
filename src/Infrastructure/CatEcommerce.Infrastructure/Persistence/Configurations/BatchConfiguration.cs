using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-IMP-001 §8.6 / §12.1. A batch stores its lifecycle status only -- <b>"expired" is never a column</b> (ADR-101: it is derived from
/// <c>expires_on</c> in the branch's local date). Stock is not here either: it is the ledger's projection. <c>ix_batch_receipt_lot</c> treats a
/// null expiry as one value (<c>NULLS NOT DISTINCT</c>), so the same undated lot twice in one receipt is refused too.
/// </summary>
public sealed class BatchConfiguration : IEntityTypeConfiguration<BatchAggregate>
{
    public void Configure(EntityTypeBuilder<BatchAggregate> builder)
    {
        builder.ConfigureInventoryRoot("batch");

        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.SkuCode).HasColumnName("sku_code").HasMaxLength(60).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.StockUnit).HasColumnName("stock_unit").HasMaxLength(20).IsRequired();
        builder.Property(x => x.StockClass).HasEnumeration("stock_class");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.LotNumber).HasColumnName("lot_number").HasMaxLength(60).IsRequired();
        builder.Property(x => x.ManufacturedOn).HasColumnName("manufactured_on").HasColumnType("date");
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").HasColumnType("date");
        builder.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
        builder.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
        builder.Property(x => x.ConsignmentId).HasColumnName("consignment_id");
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id");
        builder.Property(x => x.OriginCountryCode).HasColumnName("origin_country_code").HasMaxLength(2).IsFixedLength();
        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ReceivedQuantity).HasColumnName("received_quantity").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.PurchaseUnitCostAmount).HasColumnName("purchase_unit_cost_amount").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.PurchaseUnitCostCurrency).HasColumnName("purchase_unit_cost_currency").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.PurchaseUnitCostBase).HasColumnName("purchase_unit_cost_base").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.StatusReason).HasColumnName("status_reason").HasMaxLength(1000);
        builder.Property(x => x.StatusChangedOn).HasColumnName("status_changed_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.StatusChangedBy).HasColumnName("status_changed_by").IsRequired();

        builder.HasOne<GoodsReceiptAggregate>().WithMany().HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.GoodsReceiptId, x.SkuId, x.LotNumber, x.ExpiresOn }).IsUnique().AreNullsDistinct(false).HasDatabaseName("ix_batch_receipt_lot");
        builder.HasIndex(x => new { x.SkuId, x.Status, x.ExpiresOn }).HasDatabaseName("ix_batch_sku_status_expires_on");
        builder.HasIndex(x => x.ConsignmentId).HasDatabaseName("ix_batch_consignment");
        builder.HasIndex(x => x.LotNumber).HasDatabaseName("ix_batch_lot_number");
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_batch_branch_status");

        builder.OwnsMany(x => x.StatusHistory, history =>
        {
            history.ToTable("batch_status_change", InventoryMapping.Schema);
            history.WithOwner().HasForeignKey("batch_id");
            history.HasKey("Id");
            history.Property(h => h.Id).ValueGeneratedNever();
            history.Property(h => h.From).HasEnumeration("from_status");
            history.Property(h => h.To).HasEnumeration("to_status");
            history.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(1000);
            history.Property(h => h.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            history.Property(h => h.ChangedBy).HasColumnName("changed_by").IsRequired();
            history.Property(h => h.Sequence).HasColumnName("sequence").IsRequired();
        });
        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The owned recall record (section 8.12): ONE, in the batch's own row -- so declaring a recall is a single-row update that every in-flight
        // consumption is serialised against (FI-5). Replaced if a withdrawn batch is recalled again; the status history keeps the earlier one.
        builder.OwnsOne(x => x.CurrentRecall, recall =>
        {
            recall.Property(r => r.RecallNumber).HasColumnName("recall_number").HasMaxLength(30);
            recall.Property(r => r.Reason).HasColumnName("recall_reason").HasMaxLength(RecallRules.ReasonMaxLength);
            recall.Property(r => r.CustomerAdvice).HasColumnName("recall_customer_advice").HasMaxLength(RecallRules.AdviceMaxLength);
            recall.Property(r => r.DeclaredOn).HasColumnName("recall_declared_on").HasColumnType("timestamptz");
            recall.Property(r => r.DeclaredBy).HasColumnName("recall_declared_by");
            recall.Property(r => r.NotifiedOn).HasColumnName("recall_notified_on").HasColumnType("timestamptz");
            recall.Property(r => r.NotifiedBy).HasColumnName("recall_notified_by");
            recall.Property(r => r.NotifiedRecipientCount).HasColumnName("recall_notified_recipient_count");
            recall.Property(r => r.NotificationRuns).HasColumnName("recall_notification_runs");
            recall.Property(r => r.ClosedOn).HasColumnName("recall_closed_on").HasColumnType("timestamptz");
            recall.Property(r => r.ClosingNote).HasColumnName("recall_closing_note").HasMaxLength(1000);
            recall.HasIndex(r => r.RecallNumber).IsUnique().HasDatabaseName("ix_batch_recall_number").HasFilter("recall_number IS NOT NULL");
        });
    }
}
