using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-XCT-001 section 8.5 / 12.1 -- <c>ops.restore_test_record</c> and its owned <c>ops.restore_check_result</c>. Audit class, Forbidden.</summary>
public sealed class RestoreTestRecordConfiguration : IEntityTypeConfiguration<RestoreTestRecordAggregate>
{
    public void Configure(EntityTypeBuilder<RestoreTestRecordAggregate> builder)
    {
        builder.ToTable("restore_test_record", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PerformedOn).HasColumnName("performed_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.PerformedByStaffId).HasColumnName("performed_by_staff_id").IsRequired();
        builder.Property(x => x.Environment).HasColumnName("environment").HasMaxLength(RestoreTestRecordAggregate.MaxEnvironmentLength).IsRequired();
        builder.Property(x => x.BackupSetLabel).HasColumnName("backup_set_label").HasMaxLength(RestoreTestRecordAggregate.MaxBackupSetLabelLength).IsRequired();
        builder.Property(x => x.SimulatedFailureAt).HasColumnName("simulated_failure_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RecoveredTo).HasColumnName("recovered_to").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RestoreStartedAt).HasColumnName("restore_started_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RestoreCompletedAt).HasColumnName("restore_completed_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.AchievedRpoMinutes).HasColumnName("achieved_rpo_minutes").HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(x => x.AchievedRtoMinutes).HasColumnName("achieved_rto_minutes").HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<RestoreTestOutcome>(v)).IsRequired();
        builder.Property(x => x.DrillOutput).HasColumnName("drill_output").HasMaxLength(RestoreTestRecordAggregate.MaxDrillOutputLength).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(RestoreTestRecordAggregate.MaxNotesLength);
        builder.Property(x => x.SupersedesId).HasColumnName("supersedes_id");

        builder.OwnsMany(x => x.Checks, check =>
        {
            check.ToTable("restore_check_result", "ops");
            check.WithOwner().HasForeignKey("restore_test_record_id");
            check.HasKey(c => c.Id);
            check.Property(c => c.Id).ValueGeneratedNever();
            check.Property(c => c.Name).HasColumnName("name").HasMaxLength(RestoreCheckResult.MaxNameLength).IsRequired();
            check.Property(c => c.Passed).HasColumnName("passed").IsRequired();
            check.Property(c => c.Detail).HasColumnName("detail").HasMaxLength(RestoreCheckResult.MaxDetailLength);
        });
        builder.Navigation(x => x.Checks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.PerformedOn).HasDatabaseName("ix_restore_test_record_performed_on");
        builder.HasIndex(x => new { x.Outcome, x.PerformedOn }).HasDatabaseName("ix_restore_test_record_outcome_performed_on");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
