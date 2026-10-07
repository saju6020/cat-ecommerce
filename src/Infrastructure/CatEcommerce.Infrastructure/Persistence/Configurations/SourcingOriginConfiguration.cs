using CatEcommerce.Domain.Aggregates.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-SHP-001 section 12.1.</summary>
public sealed class SourcingOriginConfiguration : IEntityTypeConfiguration<SourcingOriginAggregate>
{
    public void Configure(EntityTypeBuilder<SourcingOriginAggregate> builder)
    {
        builder.ToTable("sourcing_origin", "product");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.HasIndex(x => x.CountryCode).IsUnique().HasDatabaseName("ix_sourcing_origin_country");

        builder.Property(x => x.SourcingNotes).HasColumnName("sourcing_notes").HasMaxLength(2000);
        builder.Property(x => x.IsFeatured).HasColumnName("is_featured").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
