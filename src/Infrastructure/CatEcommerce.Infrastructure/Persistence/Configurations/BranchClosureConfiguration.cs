using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class BranchClosureConfiguration : IEntityTypeConfiguration<BranchClosureAggregate>
{
    public void Configure(EntityTypeBuilder<BranchClosureAggregate> builder)
    {
        builder.ToTable("branch_closure", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndsOn).HasColumnName("ends_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind")
            .HasConversion(k => k.Value, v => Enumeration.FromValue<ClosureKind>(v)).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PartialFrom).HasColumnName("partial_from").HasColumnType("time");
        builder.Property(x => x.PartialTo).HasColumnName("partial_to").HasColumnType("time");
        builder.Property(x => x.IsCancelled).HasColumnName("is_cancelled").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.StartsOn, x.EndsOn }).HasDatabaseName("ix_branch_closure_branch_range");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
