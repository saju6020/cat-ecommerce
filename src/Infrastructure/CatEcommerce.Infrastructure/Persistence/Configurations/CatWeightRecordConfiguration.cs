using CatEcommerce.Domain.Aggregates.Cat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Separate from cat -- .ai/bounded-contexts.md rule 3 names weight history as unbounded.
/// HO-CAT-001 section 12.1.
/// </summary>
public sealed class CatWeightRecordConfiguration : IEntityTypeConfiguration<CatWeightRecordAggregate>
{
    public void Configure(EntityTypeBuilder<CatWeightRecordAggregate> builder)
    {
        builder.ToTable("cat_weight_record", "clinic");
        builder.HasKey(x => x.Id);
        // See CatConfiguration for why every Id needs ValueGeneratedNever().
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CatId).IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnType("date").IsRequired();
        builder.Property(x => x.RecordedByStaffId).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.OwnsOne(x => x.Weight, w =>
        {
            w.Property(p => p.Kilograms).HasColumnName("weight_kg").HasPrecision(5, 2).IsRequired();
        });

        builder.HasIndex(x => new { x.CatId, x.RecordedOn }); // drives the weight series read

        // See CatConfiguration for why Tags/Language/ServiceId must be optional.
        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
