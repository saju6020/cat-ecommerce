using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class ProductRatingSummaryConfiguration : IEntityTypeConfiguration<ProductRatingSummaryRow>
{
    public void Configure(EntityTypeBuilder<ProductRatingSummaryRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("product_rating_summary", "product");

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.RatingAverage).HasColumnName("rating_average");
        builder.Property(x => x.RatingCount).HasColumnName("rating_count");
    }
}
