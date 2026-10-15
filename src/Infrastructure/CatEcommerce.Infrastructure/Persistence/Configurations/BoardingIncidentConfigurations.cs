using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BRD-001 section 12.1. The escalation pass reads only open incidents, so its partial index is filtered on the enumeration value (never a literal).</summary>
public sealed class BoardingIncidentConfiguration : IEntityTypeConfiguration<BoardingIncidentAggregate>
{
    public void Configure(EntityTypeBuilder<BoardingIncidentAggregate> builder)
    {
        builder.ToTable("incident", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StayId).HasColumnName("stay_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Severity).HasColumnName("severity").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(IncidentRules.MaxDescriptionLength).IsRequired();
        builder.Property(x => x.ImmediateAction).HasColumnName("immediate_action").HasMaxLength(IncidentRules.MaxImmediateActionLength).IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ReportedBy).HasColumnName("reported_by").IsRequired();
        builder.Property(x => x.ReportedOn).HasColumnName("reported_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.OwnerNotified).HasColumnName("owner_notified").IsRequired();
        builder.Property(x => x.OwnerAcknowledgedOn).HasColumnName("owner_acknowledged_on").HasColumnType("timestamptz");
        builder.Property(x => x.AcknowledgedVia).HasColumnName("acknowledged_via")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<AcknowledgementChannel>(v.Value));
        builder.Property(x => x.EscalatedOn).HasColumnName("escalated_on").HasColumnType("timestamptz");
        builder.Property(x => x.PhoneCallOn).HasColumnName("phone_call_on").HasColumnType("timestamptz");
        builder.Property(x => x.PhoneCallBy).HasColumnName("phone_call_by");
        builder.Property(x => x.PhoneCallOutcome).HasColumnName("phone_call_outcome").HasMaxLength(IncidentRules.MaxPhoneOutcomeLength);
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ResolvedOn).HasColumnName("resolved_on").HasColumnType("timestamptz");
        builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(x => x.Resolution).HasColumnName("resolution").HasMaxLength(IncidentRules.MaxResolutionLength);
        builder.Ignore(x => x.NeedsPhoneCall);

        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_incident_branch_status");
        builder.HasIndex(x => new { x.StayId, x.ReportedOn }).HasDatabaseName("ix_incident_stay_reported");
        builder.HasIndex(x => new { x.Severity, x.OwnerAcknowledgedOn, x.EscalatedOn }).HasDatabaseName("ix_incident_escalation")
            .HasFilter($"status = {IncidentStatus.Open.Value}");
    }
}

public sealed class StayMediaConfiguration : IEntityTypeConfiguration<StayMediaAggregate>
{
    public void Configure(EntityTypeBuilder<StayMediaAggregate> builder)
    {
        builder.ToTable("stay_media", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StayId).HasColumnName("stay_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id");
        builder.Property(x => x.IncidentId).HasColumnName("incident_id");
        builder.Property(x => x.FileId).HasColumnName("file_id").IsRequired();
        builder.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(IncidentRules.MaxCaptionLength);
        builder.Property(x => x.CapturedOnLocalDate).HasColumnName("captured_on_local_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.UploadedBy).HasColumnName("uploaded_by").IsRequired();
        builder.Property(x => x.UploadedOn).HasColumnName("uploaded_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.HiddenReason).HasColumnName("hidden_reason").HasMaxLength(IncidentRules.MaxSkipReasonLength);

        // One stored file is one diary item (the same discipline as clinic.cat_photo.file_id): a second reference to a file is refused by the index.
        builder.HasIndex(x => x.FileId).IsUnique().HasDatabaseName("ix_stay_media_file_id");
        builder.HasIndex(x => new { x.StayId, x.CapturedOnLocalDate }).HasDatabaseName("ix_stay_media_stay_date");
        builder.HasIndex(x => x.UploadedOn).HasDatabaseName("ix_stay_media_purge")
            .HasFilter($"status <> {StayMediaStatus.Purged.Value} AND incident_id IS NULL");
    }
}
