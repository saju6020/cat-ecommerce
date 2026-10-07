using CatEcommerce.Domain.Aggregates.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1 -- append-only (ADR-080).</summary>
public sealed class SkuPriceConfiguration : IEntityTypeConfiguration<SkuPriceAggregate>
{
    public void Configure(EntityTypeBuilder<SkuPriceAggregate> builder)
    {
        builder.ToTable("sku_price", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.SetBy).HasColumnName("set_by").IsRequired();

        builder.OwnsOne(x => x.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            money.Property(m => m.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        });

        // A single btree index serves both directions in PostgreSQL (11+ scans it backwards just
        // as efficiently), so the "(sku_id, effective_from DESC)" access pattern section 12.1
        // calls for is this same unique index -- no second, redundant index is created.
        builder.HasIndex(x => new { x.SkuId, x.EffectiveFrom }).IsUnique().HasDatabaseName("ix_sku_price_sku_effective");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
