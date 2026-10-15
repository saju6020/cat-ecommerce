using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1 -- keyless, mapped to the view created by the
/// <c>AddStorefrontCatalogue</c> migration's raw SQL. Never modified from the read side.</summary>
public sealed class StorefrontCatalogueEntryConfiguration : IEntityTypeConfiguration<StorefrontCatalogueEntry>
{
    public void Configure(EntityTypeBuilder<StorefrontCatalogueEntry> builder)
    {
        builder.HasNoKey();
        builder.ToView("storefront_catalogue_entry", "product");

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Slug).HasColumnName("slug");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Brand).HasColumnName("brand");
        builder.Property(x => x.Category).HasColumnName("category");
        builder.Property(x => x.Subcategory).HasColumnName("subcategory");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.CountryOfOriginCode).HasColumnName("country_of_origin_code");
        builder.Property(x => x.LifeStage).HasColumnName("life_stage");
        builder.Property(x => x.ProteinSource).HasColumnName("protein_source");
        builder.Property(x => x.PrescriptionRequired).HasColumnName("prescription_required");
        builder.Property(x => x.SearchText).HasColumnName("search_text");
        builder.Property(x => x.FirstPublishedOn).HasColumnName("first_published_on");
        builder.Property(x => x.MinPrice).HasColumnName("min_price");
        builder.Property(x => x.MaxPrice).HasColumnName("max_price");
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code");
        builder.Property(x => x.IsOnSale).HasColumnName("is_on_sale");
        builder.Property(x => x.RatingAverage).HasColumnName("rating_average");
        builder.Property(x => x.RatingCount).HasColumnName("rating_count");
        builder.Property(x => x.UnitsSold90d).HasColumnName("units_sold_90d");
        builder.Property(x => x.PrimaryImageFileId).HasColumnName("primary_image_file_id");
        builder.Property(x => x.PrimaryImageAltText).HasColumnName("primary_image_alt_text");
    }
}
