using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §12.1 -- <c>stock_position</c>: keyless, <c>ExcludeFromMigrations</c> (created by the raw SQL of
/// <c>InventoryLedgerSql</c>), so EF can read the projection and cannot write it (ADR-099).</summary>
public sealed class StockPositionRowConfiguration : IEntityTypeConfiguration<StockPositionEntry>
{
    public void Configure(EntityTypeBuilder<StockPositionEntry> builder)
    {
        builder.HasNoKey();
        builder.ToTable("stock_position", "inventory", t => t.ExcludeFromMigrations());

        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.OnHand).HasColumnName("on_hand").HasPrecision(12, 3);
        builder.Property(x => x.Reserved).HasColumnName("reserved").HasPrecision(12, 3);
        builder.Property(x => x.LastMovementOn).HasColumnName("last_movement_on").HasColumnType("timestamptz");
    }
}
