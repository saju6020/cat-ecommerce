using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<CatEcommerce.Domain.Aggregates.Product.ProductAggregate>
{
    public void Configure(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.ToTable("product", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion(v => v.Value, v => Enumeration.FromValue<ProductKind>(v)).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(ProductRules.NameMaxLength).IsRequired();
        builder.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(ProductRules.BrandMaxLength).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(ProductRules.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasConversion(v => v.Value, v => Enumeration.FromValue<ProductCategory>(v)).IsRequired();
        builder.Property(x => x.Subcategory).HasColumnName("subcategory")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<ProductSubcategory>(v.Value));
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(ProductRules.SlugMaxLength).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ix_product_slug");

        builder.Property(x => x.Status).HasColumnName("status").HasConversion(v => v.Value, v => Enumeration.FromValue<ProductStatus>(v)).IsRequired();
        builder.Property(x => x.FirstPublishedOn).HasColumnName("first_published_on").HasColumnType("timestamptz");
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.CountryOfOriginCode).HasColumnName("country_of_origin_code").HasMaxLength(2).IsFixedLength();
        builder.Property(x => x.PrescriptionRequired).HasColumnName("prescription_required").IsRequired();
        builder.Property(x => x.IsExpiryTracked).HasColumnName("is_expiry_tracked").IsRequired();
        builder.Property(x => x.ListedOnStorefront).HasColumnName("listed_on_storefront").IsRequired();
        builder.Property(x => x.SearchText).HasColumnName("search_text").HasColumnType("text").IsRequired();

        builder.Property(x => x.Tags).HasColumnName("tags").HasColumnType("text[]").IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        ConfigureHealthFocus(builder);
        ConfigureFoodDetails(builder);

        builder.HasIndex(x => new { x.Status, x.ListedOnStorefront, x.Category, x.Subcategory }).HasDatabaseName("ix_product_status_listed_category_subcategory");
        // HO-SHP-001 §12.1: the storefront filter indexes cover Active products only (the predicate is
        // ProductStatus.Active, interpolated from the enumeration), and "newest" is DESC.
        builder.HasIndex(x => x.CountryOfOriginCode).HasDatabaseName("ix_product_country_of_origin").HasFilter($"status = {ProductStatus.Active.Value}");
        builder.HasIndex(x => x.Brand).HasDatabaseName("ix_product_brand").HasFilter($"status = {ProductStatus.Active.Value}");
        builder.HasIndex(x => x.FirstPublishedOn).IsDescending().HasDatabaseName("ix_product_first_published_on_desc");

        ConfigureSkus(builder);
        ConfigureImages(builder);
        ConfigureBundleComponents(builder);
        ConfigureRelatedProducts(builder);
        ConfigureTranslations(builder);

        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }

    private static void ConfigureHealthFocus(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        var comparer = new ValueComparer<IReadOnlyList<HealthFocus>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.Value)),
            v => v.ToList());

        var healthFocusProperty = builder.Property(x => x.HealthFocus)
            .HasColumnName("health_focus")
            .HasColumnType("integer[]")
            .HasConversion(
                v => v.Select(h => h.Value).ToArray(),
                v => v.Select(Enumeration.FromValue<HealthFocus>).ToList())
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        healthFocusProperty.Metadata.SetValueComparer(comparer);
    }

    private static void ConfigureFoodDetails(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsOne(x => x.FoodDetails, food =>
        {
            food.Property(f => f.Manufacturer).HasColumnName("food_manufacturer").HasMaxLength(ProductRules.ManufacturerMaxLength);
            food.Property(f => f.LifeStage).HasColumnName("food_life_stage")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<LifeStage>(v.Value));
            food.Property(f => f.ProteinSource).HasColumnName("food_protein_source")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<ProteinSource>(v.Value));
            food.HasIndex(f => f.LifeStage).HasDatabaseName("ix_product_food_life_stage").HasFilter($"status = {ProductStatus.Active.Value}");
            food.HasIndex(f => f.ProteinSource).HasDatabaseName("ix_product_food_protein_source").HasFilter($"status = {ProductStatus.Active.Value}");
            food.Property(f => f.IngredientList).HasColumnName("food_ingredient_list").HasMaxLength(ProductRules.IngredientListMaxLength);
            food.Property(f => f.FeedingGuide).HasColumnName("food_feeding_guide").HasMaxLength(ProductRules.FeedingGuideMaxLength);
            food.Property(f => f.StorageInstructions).HasColumnName("food_storage_instructions").HasMaxLength(ProductRules.StorageInstructionsMaxLength);

            // Flat columns, not a nested owned type -- see FoodDetails' own doc comment.
            food.Property(f => f.AnalysisProteinPercent).HasColumnName("food_analysis_protein_percent").HasPrecision(5, 2);
            food.Property(f => f.AnalysisFatPercent).HasColumnName("food_analysis_fat_percent").HasPrecision(5, 2);
            food.Property(f => f.AnalysisFibrePercent).HasColumnName("food_analysis_fibre_percent").HasPrecision(5, 2);
            food.Property(f => f.AnalysisMoisturePercent).HasColumnName("food_analysis_moisture_percent").HasPrecision(5, 2);
            food.Ignore(f => f.GuaranteedAnalysis);
        });
    }

    private static void ConfigureSkus(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsMany(x => x.Skus, sku =>
        {
            sku.ToTable("sku", "product");
            sku.WithOwner().HasForeignKey("product_id");
            sku.HasKey("Id");
            sku.Property(s => s.Id).ValueGeneratedNever();
            sku.HasIndex(s => s.Id).IsUnique().HasDatabaseName("ix_sku_id");

            sku.Property(s => s.Code).HasColumnName("code").HasMaxLength(ProductRules.SkuCodeMaxLength).IsRequired();
            sku.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ix_sku_code");

            sku.Property(s => s.Barcode).HasColumnName("barcode").HasMaxLength(ProductRules.BarcodeMaxLength);
            sku.HasIndex(s => s.Barcode).IsUnique().HasDatabaseName("ix_sku_barcode").HasFilter("barcode IS NOT NULL");

            sku.Property(s => s.SizeOrWeight).HasColumnName("size_or_weight").HasMaxLength(ProductRules.SizeOrWeightMaxLength);
            sku.Property(s => s.Flavour).HasColumnName("flavour").HasMaxLength(ProductRules.FlavourMaxLength);
            sku.Property(s => s.Colour).HasColumnName("colour").HasMaxLength(ProductRules.ColourMaxLength);
            sku.Property(s => s.PackQuantity).HasColumnName("pack_quantity");
            sku.Property(s => s.ShippingWeightGrams).HasColumnName("shipping_weight_grams");
            sku.Property(s => s.Status).HasColumnName("status").HasConversion(v => v.Value, v => Enumeration.FromValue<SkuStatus>(v)).IsRequired();

            sku.HasIndex("product_id", nameof(Sku.Status)).HasDatabaseName("ix_sku_product_status");
        });
        builder.Navigation(x => x.Skus).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureImages(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsMany(x => x.Images, image =>
        {
            image.ToTable("product_image", "product");
            image.WithOwner().HasForeignKey("product_id");
            image.HasKey("Id");
            image.Property(i => i.Id).ValueGeneratedNever();
            image.Property(i => i.SkuId).HasColumnName("sku_id");
            image.Property(i => i.FileId).HasColumnName("file_id").IsRequired();
            image.Property(i => i.AltText).HasColumnName("alt_text").HasMaxLength(ProductRules.AltTextMaxLength).IsRequired();
            image.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();
            image.Property(i => i.IsPrimary).HasColumnName("is_primary").IsRequired();
        });
        builder.Navigation(x => x.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureBundleComponents(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsMany(x => x.BundleComponents, component =>
        {
            component.ToTable("bundle_component", "product");
            component.WithOwner().HasForeignKey("product_id");
            component.HasKey("Id");
            component.Property(c => c.Id).ValueGeneratedNever();
            component.Property(c => c.ComponentSkuId).HasColumnName("component_sku_id").IsRequired();
            component.Property(c => c.Quantity).HasColumnName("quantity").IsRequired();
            component.HasIndex(c => c.ComponentSkuId).HasDatabaseName("ix_bundle_component_component_sku_id");
        });
        builder.Navigation(x => x.BundleComponents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureRelatedProducts(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsMany(x => x.RelatedProducts, link =>
        {
            link.ToTable("related_product", "product");
            link.WithOwner().HasForeignKey("product_id");
            link.HasKey("Id");
            link.Property(l => l.Id).ValueGeneratedNever();
            link.Property(l => l.RelatedProductId).HasColumnName("related_product_id").IsRequired();
            link.Property(l => l.Kind).HasColumnName("kind").HasConversion(v => v.Value, v => Enumeration.FromValue<RelatedProductKind>(v)).IsRequired();
            link.Property(l => l.SortOrder).HasColumnName("sort_order").IsRequired();
        });
        builder.Navigation(x => x.RelatedProducts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureTranslations(EntityTypeBuilder<CatEcommerce.Domain.Aggregates.Product.ProductAggregate> builder)
    {
        builder.OwnsMany(x => x.Translations, translation =>
        {
            translation.ToTable("product_translation", "product");
            translation.WithOwner().HasForeignKey("product_id");
            translation.HasKey("Locale", "product_id");
            translation.Property(t => t.Locale).HasColumnName("locale").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Name).HasColumnName("name").HasMaxLength(ProductRules.NameMaxLength);
            translation.Property(t => t.Description).HasColumnName("description").HasMaxLength(ProductRules.DescriptionMaxLength);
            translation.Property(t => t.IngredientList).HasColumnName("ingredient_list").HasMaxLength(ProductRules.IngredientListMaxLength);
            translation.Property(t => t.FeedingGuide).HasColumnName("feeding_guide").HasMaxLength(ProductRules.FeedingGuideMaxLength);
        });
        builder.Navigation(x => x.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
