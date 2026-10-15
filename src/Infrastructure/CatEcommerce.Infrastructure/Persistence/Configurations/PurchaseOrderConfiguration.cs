using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.5 / §12.1. Receipt PROGRESS is not stored (ADR-112): there is no accepted/outstanding column on either table.
/// Quantities are <c>numeric(12,3)</c>, unit costs <c>numeric(14,4)</c> (<c>UnitCost</c>, not <c>Money</c>).</summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrderAggregate>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderAggregate> builder)
    {
        builder.ConfigureInventoryRoot("purchase_order");

        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_purchase_order_number");

        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.DeliverToLocationId).HasColumnName("deliver_to_location_id").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.ExpectedDeliveryOn).HasColumnName("expected_delivery_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.StatusReason).HasColumnName("status_reason").HasMaxLength(500);
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_purchase_order_branch_status");
        builder.HasIndex(x => x.SupplierId).HasDatabaseName("ix_purchase_order_supplier");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("purchase_order_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("purchase_order_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();

            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.SkuCode).HasColumnName("sku_code").HasMaxLength(60).IsRequired();
            line.Property(l => l.DisplayName).HasColumnName("display_name").HasMaxLength(300).IsRequired();
            line.Property(l => l.StockUnit).HasColumnName("stock_unit").HasMaxLength(20).IsRequired();
            line.Property(l => l.OrderedQuantity).HasColumnName("ordered_quantity").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.UnitCostAmount).HasColumnName("unit_cost_amount").HasPrecision(14, 4).IsRequired();

            line.HasIndex("purchase_order_id", nameof(Domain.Entities.PurchaseOrderLine.SkuId)).IsUnique().HasDatabaseName("ix_purchase_order_line_order_sku");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
