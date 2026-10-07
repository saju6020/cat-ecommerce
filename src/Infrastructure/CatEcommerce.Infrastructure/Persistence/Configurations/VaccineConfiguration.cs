using CatEcommerce.Domain.Aggregates.Vaccination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-CAT-001 section 12.1.</summary>
public sealed class VaccineConfiguration : IEntityTypeConfiguration<VaccineAggregate>
{
    public void Configure(EntityTypeBuilder<VaccineAggregate> builder)
    {
        builder.ToTable("vaccine", "clinic");
        builder.HasKey(x => x.Id);
        // See CatConfiguration for why every Id needs ValueGeneratedNever().
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ValidityMonths).IsRequired();
        builder.Property(x => x.IsCore).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsCore);

        // See CatConfiguration for why Tags/Language/ServiceId must be optional.
        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
