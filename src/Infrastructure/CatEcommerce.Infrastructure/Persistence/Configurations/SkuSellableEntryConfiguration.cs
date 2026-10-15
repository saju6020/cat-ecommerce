using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Keyless, mapped to <c>inventory.v_sku_sellable</c> (created by the AddSkuSellableSummary migration raw SQL). Never modified.</summary>
public sealed class SkuSellableEntryConfiguration : IEntityTypeConfiguration<SkuSellableEntry>
{
    public void Configure(EntityTypeBuilder<SkuSellableEntry> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_sku_sellable", "inventory");

        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").HasColumnType("date");
        builder.Property(x => x.SellableQuantity).HasColumnName("sellable_quantity").HasPrecision(14, 3);
        builder.Property(x => x.LastChangedOn).HasColumnName("last_changed_on");
    }
}
