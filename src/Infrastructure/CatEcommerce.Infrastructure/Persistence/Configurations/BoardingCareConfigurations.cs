using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BRD-001 section 12.1 -- care tasks. <c>ix_care_task_generation</c> (unique on stay, cat, local date, source key) makes generation idempotent: a second run
/// that races the first hits it and the generator treats the violation as success (ADR-224, renumbered ADR-357).</summary>
public sealed class CareTaskConfiguration : IEntityTypeConfiguration<CareTaskAggregate>
{
    public void Configure(EntityTypeBuilder<CareTaskAggregate> builder)
    {
        builder.ToTable("care_task", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StayId).HasColumnName("stay_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.UnitId).HasColumnName("unit_id").IsRequired();
        builder.Property(x => x.LocalDate).HasColumnName("local_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.DueOn).HasColumnName("due_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.DueLocalTime).HasColumnName("due_local_time").HasColumnType("time").IsRequired();
        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(IncidentRules.MaxTaskTitleLength).IsRequired();
        builder.Property(x => x.AssignedCaretakerId).HasColumnName("assigned_caretaker_id");
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");
        builder.Property(x => x.CompletedBy).HasColumnName("completed_by");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(IncidentRules.MaxTaskNoteLength);
        builder.Property(x => x.SkipReason).HasColumnName("skip_reason").HasMaxLength(IncidentRules.MaxSkipReasonLength);

        builder.HasIndex(x => new { x.StayId, x.CatId, x.LocalDate, x.SourceKey }).IsUnique().HasDatabaseName("ix_care_task_generation");
        builder.HasIndex(x => new { x.BranchId, x.LocalDate, x.Status }).HasDatabaseName("ix_care_task_branch_date_status");
        builder.HasIndex(x => new { x.AssignedCaretakerId, x.LocalDate }).HasDatabaseName("ix_care_task_caretaker_date");
    }
}

public sealed class CareObservationConfiguration : IEntityTypeConfiguration<CareObservationAggregate>
{
    public void Configure(EntityTypeBuilder<CareObservationAggregate> builder)
    {
        builder.ToTable("care_observation", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StayId).HasColumnName("stay_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.LocalDate).HasColumnName("local_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by").IsRequired();
        builder.Property(x => x.PortionEaten).HasColumnName("portion_eaten").HasEnumerationValue().IsRequired();
        builder.Property(x => x.WaterIntake).HasColumnName("water_intake").HasEnumerationValue().IsRequired();
        builder.Property(x => x.LitterOutput).HasColumnName("litter_output").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Mood).HasColumnName("mood").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(IncidentRules.MaxObservationNotesLength);

        builder.HasIndex(x => new { x.StayId, x.CatId, x.RecordedOn }).HasDatabaseName("ix_care_observation_stay_cat_recorded");
    }
}

public sealed class IsolationEntryConfiguration : IEntityTypeConfiguration<IsolationEntryAggregate>
{
    public void Configure(EntityTypeBuilder<IsolationEntryAggregate> builder)
    {
        builder.ToTable("isolation_entry", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StayId).HasColumnName("stay_id").IsRequired();
        builder.Property(x => x.UnitId).HasColumnName("unit_id").IsRequired();
        builder.Property(x => x.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
        builder.Property(x => x.EnteredOn).HasColumnName("entered_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.LeftOn).HasColumnName("left_on").HasColumnType("timestamptz");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(IncidentRules.MaxIsolationPurposeLength).IsRequired();

        builder.HasIndex(x => new { x.StayId, x.EnteredOn }).HasDatabaseName("ix_isolation_entry_stay_entered");
    }
}
