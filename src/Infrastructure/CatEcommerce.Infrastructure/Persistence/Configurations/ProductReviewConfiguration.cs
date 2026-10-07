using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1.</summary>
public sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReviewAggregate>
{
    public void Configure(EntityTypeBuilder<ProductReviewAggregate> builder)
    {
        builder.ToTable("product_review", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.SkuId).HasColumnName("sku_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Rating).HasColumnName("rating").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(ReviewRules.TitleMaxLength);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(ReviewRules.BodyMaxLength).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(v => v.Value, v => Enumeration.FromValue<ProductReviewStatus>(v)).IsRequired();
        builder.Property(x => x.SubmittedOn).HasColumnName("submitted_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ModeratedOn).HasColumnName("moderated_on").HasColumnType("timestamptz");
        builder.Property(x => x.ModeratedBy).HasColumnName("moderated_by");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<ReviewRejectionReason>(v.Value));
        builder.Property(x => x.ModerationNote).HasColumnName("moderation_note").HasMaxLength(500);

        builder.HasIndex(x => new { x.ProductId, x.Status, x.SubmittedOn }).HasDatabaseName("ix_product_review_product_status_submitted");
        builder.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique().HasDatabaseName("ix_product_review_active_per_customer_product")
            .HasFilter($"status <> {ProductReviewStatus.Withdrawn.Value}");
        builder.HasIndex(x => new { x.Status, x.SubmittedOn }).HasDatabaseName("ix_product_review_status_submitted");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
