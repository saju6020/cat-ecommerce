using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItemAggregate>
{
    public void Configure(EntityTypeBuilder<ContentItemAggregate> builder)
    {
        builder.ToTable("content_item", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion(k => k.Value, v => Enumeration.FromValue<ContentKind>(v)).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.DraftBody).HasColumnName("draft_body").HasMaxLength(50_000).IsRequired();
        builder.Property(x => x.PublishedBody).HasColumnName("published_body").HasMaxLength(50_000);
        builder.Property(x => x.PublishedOn).HasColumnName("published_on").HasColumnType("timestamptz");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(k => k.Value, v => Enumeration.FromValue<ContentStatus>(v)).IsRequired();
        builder.Property(x => x.Placement).HasColumnName("placement").HasConversion(k => k == null ? (int?)null : k.Value, v => v == null ? null : Enumeration.FromValue<BannerPlacement>(v.Value));
        builder.Property(x => x.LinkUrl).HasColumnName("link_url").HasMaxLength(2000);
        builder.Property(x => x.VisibleFrom).HasColumnName("visible_from").HasColumnType("timestamptz");
        builder.Property(x => x.VisibleTo).HasColumnName("visible_to").HasColumnType("timestamptz");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(x => new { x.Kind, x.Slug }).IsUnique().HasDatabaseName("ix_content_item_kind_slug");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
