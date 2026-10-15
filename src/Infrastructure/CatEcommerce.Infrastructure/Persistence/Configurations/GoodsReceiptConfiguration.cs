using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.6 / §12.1. A posted receipt is immutable (ADR-105); its lines are an owned collection. Quantities are
/// <c>numeric(12,3)</c>, unit costs <c>numeric(14,4)</c>, the line's exchange rate <c>numeric(18,6)</c>. <c>ix_goods_receipt_line_purchase_order_line</c>
/// is what receipt PROGRESS (ADR-112) groups by.</summary>
public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceiptAggregate>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptAggregate> builder)
    {
        builder.ConfigureInventoryRoot("goods_receipt");

        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_goods_receipt_number");

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id");
        builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Property(x => x.ConsignmentId).HasColumnName("consignment_id");
        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ReceivedBy).HasColumnName("received_by").IsRequired();
        builder.Property(x => x.DeliveryReference).HasColumnName("delivery_reference").HasMaxLength(60);

        builder.HasIndex(x => new { x.BranchId, x.ReceivedOn }).HasDatabaseName("ix_goods_receipt_branch_received_on");
        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("ix_goods_receipt_purchase_order");
        builder.HasIndex(x => x.ConsignmentId).HasDatabaseName("ix_goods_receipt_consignment");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("goods_receipt_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("goods_receipt_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();

            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.SkuCode).HasColumnName("sku_code").HasMaxLength(60).IsRequired();
            line.Property(l => l.DisplayName).HasColumnName("display_name").HasMaxLength(300).IsRequired();
            line.Property(l => l.StockUnit).HasColumnName("stock_unit").HasMaxLength(20).IsRequired();
            line.Property(l => l.PurchaseOrderLineId).HasColumnName("purchase_order_line_id");
            line.Property(l => l.LotNumber).HasColumnName("lot_number").HasMaxLength(InventoryRules.MaxLotNumberLength).IsRequired();
            line.Property(l => l.ManufacturedOn).HasColumnName("manufactured_on").HasColumnType("date");
            line.Property(l => l.ExpiresOn).HasColumnName("expires_on").HasColumnType("date");
            line.Property(l => l.QuantityAccepted).HasColumnName("quantity_accepted").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.QuantityRejected).HasColumnName("quantity_rejected").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.RejectionReason).HasNullableEnumeration("rejection_reason");
            line.Property(l => l.RejectionNote).HasColumnName("rejection_note").HasMaxLength(InventoryRules.NoteMaxLength);
            line.Property(l => l.LocationId).HasColumnName("location_id").IsRequired();
            line.Property(l => l.HoldForInspection).HasColumnName("hold_for_inspection").IsRequired();
            line.Property(l => l.UnitCostAmount).HasColumnName("unit_cost_amount").HasPrecision(14, 4);
            line.Property(l => l.UnitCostCurrency).HasColumnName("unit_cost_currency").HasMaxLength(3).IsFixedLength();
            line.Property(l => l.ExchangeRateToBase).HasColumnName("exchange_rate_to_base").HasPrecision(18, 6);
            line.Property(l => l.BatchId).HasColumnName("batch_id");

            line.HasIndex(l => l.PurchaseOrderLineId).HasDatabaseName("ix_goods_receipt_line_purchase_order_line");
            line.HasIndex(l => l.BatchId).HasDatabaseName("ix_goods_receipt_line_batch");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
