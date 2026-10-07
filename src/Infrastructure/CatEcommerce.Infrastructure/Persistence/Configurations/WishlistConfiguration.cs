using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1 -- <c>Id == CustomerId</c> (so the primary key is the
/// one-wishlist-per-customer rule and a first-write race is a unique violation the filter maps to
/// 409); items are an owned table unique per (wishlist, SKU).</summary>
public sealed class WishlistConfiguration : IEntityTypeConfiguration<WishlistAggregate>
{
    public void Configure(EntityTypeBuilder<WishlistAggregate> builder)
    {
        builder.ToTable("wishlist", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.OwnsMany(x => x.Items, item =>
        {
            item.ToTable("wishlist_item", "product");
            item.WithOwner().HasForeignKey("wishlist_id");
            item.HasKey("wishlist_id", nameof(WishlistItem.SkuId));
            item.Property(i => i.SkuId).HasColumnName("sku_id").ValueGeneratedNever().IsRequired();
            item.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();
            item.Property(i => i.AddedOn).HasColumnName("added_on").HasColumnType("timestamptz").IsRequired();
            item.HasIndex(i => i.SkuId).HasDatabaseName("ix_wishlist_item_sku_id");
        });
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
