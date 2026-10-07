using CatEcommerce.Domain.Aggregates.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1 -- Id == ProductId (section 8.7). System-written; no
/// application-level uniqueness beyond the primary key.</summary>
public sealed class ProductSalesStatsConfiguration : IEntityTypeConfiguration<ProductSalesStatsAggregate>
{
    public void Configure(EntityTypeBuilder<ProductSalesStatsAggregate> builder)
    {
        builder.ToTable("product_sales_stats", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.UnitsSold90d).HasColumnName("units_sold_90d").IsRequired();
        builder.Property(x => x.AsOf).HasColumnName("as_of").HasColumnType("timestamptz").IsRequired();

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
