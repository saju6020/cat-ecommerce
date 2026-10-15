using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §12.1 -- keyless, mapped to the view created by the <c>AddInventoryReservations</c> migration's raw SQL
/// (<c>InventoryLedgerSql.CreateSellableStockView</c>). Never modified.</summary>
public sealed class SellableStockEntryConfiguration : IEntityTypeConfiguration<SellableStockEntry>
{
    public void Configure(EntityTypeBuilder<SellableStockEntry> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_sellable_stock", "inventory");

        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.SellableQuantity).HasColumnName("sellable_quantity").HasPrecision(14, 3);
        builder.Property(x => x.NextExpiresOn).HasColumnName("next_expires_on").HasColumnType("date");
    }
}
