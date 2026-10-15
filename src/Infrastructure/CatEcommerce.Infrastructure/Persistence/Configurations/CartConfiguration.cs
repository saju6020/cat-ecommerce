using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-ORD-001 §12.1 (Phase C) -- <c>ordering.cart</c> with its owned <c>cart_line</c>. <c>ix_cart_active_customer</c> is the "one active cart per
/// customer" rule (a first-write race from two devices is a <c>409</c>); <c>ix_cart_guest_token</c> keys a guest cart by the SHA-256 of its cookie;
/// <c>ix_cart_guest_purge</c> serves the retention sweep. The status predicates are built from the enumeration (U-X5 of Shop's pattern).
/// </summary>
public sealed class CartConfiguration : IEntityTypeConfiguration<CartAggregate>
{
    public void Configure(EntityTypeBuilder<CartAggregate> builder)
    {
        builder.ConfigureOrderingRoot("cart");

        builder.Property(x => x.OwnerKind).HasEnumeration("owner_kind");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.GuestTokenHash).HasColumnName("guest_token_hash").HasColumnType("char(64)");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.MergedIntoCartId).HasColumnName("merged_into_cart_id");
        builder.Property(x => x.LastActivityOn).HasInstant("last_activity_on").IsRequired();

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("cart_line", OrderingMapping.Schema);
            line.WithOwner().HasForeignKey("cart_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.CatId).HasColumnName("cat_id");
            line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
            line.Property(l => l.PriceWhenAdded).HasMoney("price_when_added").IsRequired();
            line.Property(l => l.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
            line.Property(l => l.AddedOn).HasInstant("added_on").IsRequired();
            line.Property(l => l.UpdatedOn).HasInstant("updated_on").IsRequired();
            line.HasIndex("cart_id").HasDatabaseName("ix_cart_line_cart");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("ix_cart_active_customer")
            .HasFilter($"status = {CartStatus.Active.Value} AND customer_id IS NOT NULL");
        builder.HasIndex(x => x.GuestTokenHash).IsUnique().HasDatabaseName("ix_cart_guest_token")
            .HasFilter("guest_token_hash IS NOT NULL");
        builder.HasIndex(x => new { x.OwnerKind, x.Status, x.LastActivityOn }).HasDatabaseName("ix_cart_guest_purge");
    }
}
