using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-EMR-001 §8.4 -- <c>clinic.visit_addendum</c>: append-only (a trigger refuses every UPDATE and
/// DELETE, ADR-118), <b>no customer id</b>, never deleted.</summary>
public sealed class VisitAddendumConfiguration : IEntityTypeConfiguration<VisitAddendumAggregate>
{
    public void Configure(EntityTypeBuilder<VisitAddendumAggregate> builder)
    {
        builder.ToTable("visit_addendum", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.Text).HasColumnName("text").HasMaxLength(ClinicalNoteRules.MaxAddendumLength).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(ClinicalNoteRules.MaxAddendumReasonLength);
        builder.Property(x => x.AuthorStaffId).HasColumnName("author_staff_id").IsRequired();
        builder.Property(x => x.AddedOn).HasColumnName("added_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.VisitId, x.AddedOn }).HasDatabaseName("ix_visit_addendum_visit_added_on");
    }
}

/// <summary>HO-EMR-001 §8.4 / ADR-122 -- <c>clinic.consultation_draft</c>: one working copy per
/// <c>(visit, author)</c>, overwritten in place, never deleted, never read by anyone but its author.
/// Excluded from audit change capture (<c>AuditCaptureExclusions</c>). The problem list is code ids only,
/// as <c>jsonb</c> (§12.1).</summary>
public sealed class ConsultationDraftConfiguration : IEntityTypeConfiguration<ConsultationDraftAggregate>
{
    public void Configure(EntityTypeBuilder<ConsultationDraftAggregate> builder)
    {
        builder.ToTable("consultation_draft", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.AuthorStaffId).HasColumnName("author_staff_id").IsRequired();
        builder.Property(x => x.Subjective).HasColumnName("subjective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Assessment).HasColumnName("assessment").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Plan).HasColumnName("plan").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.DischargeInstructions).HasColumnName("discharge_instructions").HasMaxLength(ClinicalNoteRules.MaxDischargeInstructionsLength).IsRequired();
        // jsonb written and read explicitly: a List<Guid> otherwise maps as a uuid[] primitive collection.
        builder.Property(x => x.ProblemCodeIds).HasColumnName("problem_code_ids").HasColumnType("jsonb").IsRequired()
            .HasConversion(
                ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null) ?? new List<Guid>(),
                new ValueComparer<List<Guid>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    ids => ids.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                    ids => ids.ToList()));
        builder.Property(x => x.SavedOn).HasColumnName("saved_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.VisitId, x.AuthorStaffId }).IsUnique().HasDatabaseName("ix_consultation_draft_visit_author");
    }
}
