using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-XCT-001 section 8.4 / 12.1 -- <c>ops.retention_run</c> and its owned <c>ops.retention_pass_result</c>.</summary>
public sealed class RetentionRunConfiguration : IEntityTypeConfiguration<RetentionRunAggregate>
{
    public void Configure(EntityTypeBuilder<RetentionRunAggregate> builder)
    {
        builder.ToTable("retention_run", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Trigger).HasColumnName("trigger")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<RetentionRunTrigger>(v)).IsRequired();
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.StartedOn).HasColumnName("started_on").HasColumnType("timestamptz");
        builder.Property(x => x.FinishedOn).HasColumnName("finished_on").HasColumnType("timestamptz");
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<RetentionRunStatus>(v)).IsRequired();

        builder.OwnsMany(x => x.Passes, pass =>
        {
            pass.ToTable("retention_pass_result", "ops");
            pass.WithOwner().HasForeignKey("retention_run_id");
            pass.HasKey(p => p.Id);
            pass.Property(p => p.Id).ValueGeneratedNever();
            pass.Property(p => p.Kind).HasColumnName("kind")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<RetentionPassKind>(v)).IsRequired();
            pass.Property(p => p.Cutoff).HasColumnName("cutoff").HasColumnType("timestamptz");
            pass.Property(p => p.Examined).HasColumnName("examined").IsRequired();
            pass.Property(p => p.Affected).HasColumnName("affected").IsRequired();
            pass.Property(p => p.Held).HasColumnName("held").IsRequired();
            pass.Property(p => p.Failed).HasColumnName("failed").IsRequired();
            pass.Property(p => p.ErrorCodes).HasColumnName("error_codes").HasColumnType("text[]").IsRequired();
            pass.Property(p => p.DurationMs).HasColumnName("duration_ms").IsRequired();
            pass.Ignore(p => p.HadErrors);
        });
        builder.Navigation(x => x.Passes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_retention_run_status");
        builder.HasIndex(x => x.RequestedOn).HasDatabaseName("ix_retention_run_requested_on");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
