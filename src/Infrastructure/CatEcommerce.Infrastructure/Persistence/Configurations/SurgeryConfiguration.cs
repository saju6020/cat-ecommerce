using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-SRG-001 §8.2 / §12.1 -- <c>clinic.surgery</c> and its owned checklist, follow-ups and the two histories. HO-CAT-001 §12.1 / ADR-021 conventions: an enumeration is
/// stored by its <c>Value</c>; <c>ValueGeneratedNever</c> on every root AND owned id; the platform's optional base columns are not required; <c>xmin</c> is the
/// concurrency token. No foreign key to the cat, customer, appointment or staff (ADR-011) -- the exclusion constraints on the appointment are the control (ADR-068).
/// </summary>
public sealed class SurgeryConfiguration : IEntityTypeConfiguration<SurgeryAggregate>
{
    public void Configure(EntityTypeBuilder<SurgeryAggregate> builder)
    {
        builder.ToTable("surgery", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.ClinicServiceId).HasColumnName("service_id").IsRequired();
        builder.Property(x => x.AppointmentId).HasColumnName("appointment_id").IsRequired();
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.TheatreId).HasColumnName("theatre_id").IsRequired();
        builder.Property(x => x.SurgeonId).HasColumnName("surgeon_id").IsRequired();
        builder.Property(x => x.AssistantId).HasColumnName("assistant_id").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.EndsOn).HasColumnName("ends_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.AdmitBy).HasColumnName("admit_by").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.FastingInstructions).HasColumnName("fasting_instructions").HasMaxLength(SurgeryRules.FastingInstructionsMax).IsRequired();
        builder.Property(x => x.ScheduleStamp).HasColumnName("schedule_stamp").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.AsaGrade).HasColumnName("asa_grade").HasNullableEnum();
        builder.Property(x => x.AsaIsEmergency).HasColumnName("asa_is_emergency").IsRequired();
        builder.Property(x => x.AdmittedOn).HasColumnName("admitted_on").HasColumnType("timestamptz");
        builder.Property(x => x.ProcedureStartedOn).HasColumnName("procedure_started_on").HasColumnType("timestamptz");
        builder.Property(x => x.ProcedureEndedOn).HasColumnName("procedure_ended_on").HasColumnType("timestamptz");
        builder.Property(x => x.ProcedureEndedLocalDate).HasColumnName("procedure_ended_local_date").HasColumnType("date");
        builder.Property(x => x.DischargedOn).HasColumnName("discharged_on").HasColumnType("timestamptz");
        builder.Property(x => x.OwnerAtDischargeId).HasColumnName("owner_at_discharge_id"); // HO-BIL-001 Phase J: the cat's owner at discharge (null before Phase J)
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasNullableEnum();
        builder.Property(x => x.RecoveryStatus).HasColumnName("recovery_status").HasNullableEnum();
        builder.Property(x => x.PostOpInstructions).HasColumnName("post_op_instructions").HasMaxLength(SurgeryRules.PostOpInstructionsMax);
        builder.Property(x => x.CatWasDeceasedAtDischarge).HasColumnName("cat_was_deceased_at_discharge").IsRequired();
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(SurgeryRules.CancellationReasonMax);

        builder.OwnsOne(x => x.Procedure, p =>
        {
            p.Property(v => v.ServiceName).HasColumnName("procedure_name").HasMaxLength(200).IsRequired();
            p.Property(v => v.Category).HasColumnName("procedure_category").HasEnumerationValue().IsRequired();
            p.Property(v => v.DurationMinutes).HasColumnName("procedure_duration_minutes").IsRequired();
            p.Property(v => v.IsSterilisation).HasColumnName("procedure_is_sterilisation").IsRequired();
        });
        builder.Navigation(x => x.Procedure).IsRequired();

        builder.OwnsOne(x => x.PriceSnapshot, p =>
        {
            p.OwnsOne(v => v.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
                money.Property(m => m.CurrencyCode).HasColumnName("price_currency_code").HasColumnType("char(3)").IsRequired();
            });
            p.Property(v => v.Basis).HasColumnName("price_basis").HasEnumerationValue().IsRequired();
            p.Property(v => v.CampaignId).HasColumnName("campaign_id");
            p.Property(v => v.CampaignEligibilityNote).HasColumnName("campaign_eligibility_note").HasMaxLength(SurgeryRules.EligibilityNoteMax);
        });
        builder.Navigation(x => x.PriceSnapshot).IsRequired();

        builder.OwnsOne(x => x.ConsentOverride, o =>
        {
            o.Property(v => v.OverriddenBy).HasColumnName("consent_override_by");
            o.Property(v => v.OverriddenOn).HasColumnName("consent_override_on").HasColumnType("timestamptz");
            o.Property(v => v.Reason).HasColumnName("consent_override_reason").HasMaxLength(SurgeryRules.OverrideReasonMax);
        });

        builder.OwnsMany(x => x.Checklist, c =>
        {
            c.ToTable("surgery_checklist_item", "clinic");
            c.WithOwner().HasForeignKey("surgery_id");
            c.HasKey("Id");
            c.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            c.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            c.Property(p => p.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
            c.Property(p => p.CompletedBy).HasColumnName("completed_by");
            c.Property(p => p.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");
            c.Property(p => p.Note).HasColumnName("note").HasMaxLength(SurgeryRules.NoteMax);
            c.Property(p => p.WaiverReason).HasColumnName("waiver_reason").HasMaxLength(SurgeryRules.WaiverReasonMax);
            c.Property(p => p.ReferenceId).HasColumnName("reference_id");
            c.Property(p => p.ReferenceDate).HasColumnName("reference_date").HasColumnType("date");
            c.HasIndex("surgery_id").HasDatabaseName("ix_surgery_checklist_item_surgery");
        });
        builder.Metadata.FindNavigation(nameof(SurgeryAggregate.Checklist))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.FollowUps, f =>
        {
            f.ToTable("surgery_follow_up", "clinic");
            f.WithOwner().HasForeignKey("surgery_id");
            f.HasKey("Id");
            f.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            f.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            f.Property(p => p.DueOn).HasColumnName("due_on").HasColumnType("date").IsRequired();
            f.Property(p => p.WindowFrom).HasColumnName("window_from").HasColumnType("date").IsRequired();
            f.Property(p => p.WindowTo).HasColumnName("window_to").HasColumnType("date").IsRequired();
            f.Property(p => p.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
            f.Property(p => p.ClosedOn).HasColumnName("closed_on").HasColumnType("timestamptz");
            f.Property(p => p.ClosedBy).HasColumnName("closed_by");
            f.Property(p => p.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(SurgeryRules.CancellationReasonMax);
            f.HasIndex("surgery_id").HasDatabaseName("ix_surgery_follow_up_surgery");
        });
        builder.Metadata.FindNavigation(nameof(SurgeryAggregate.FollowUps))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.StatusHistory, h =>
        {
            h.ToTable("surgery_status_change", "clinic");
            h.WithOwner().HasForeignKey("surgery_id");
            h.HasKey("Id");
            h.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            h.Property(p => p.From).HasColumnName("from_status").HasNullableEnum();
            h.Property(p => p.To).HasColumnName("to_status").HasEnumerationValue().IsRequired();
            h.Property(p => p.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            h.Property(p => p.ChangedBy).HasColumnName("changed_by").IsRequired();
            h.Property(p => p.Sequence).HasColumnName("sequence").IsRequired();
            h.HasIndex("surgery_id").HasDatabaseName("ix_surgery_status_change_surgery");
        });
        builder.Metadata.FindNavigation(nameof(SurgeryAggregate.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.RecoveryHistory, h =>
        {
            h.ToTable("surgery_recovery_change", "clinic");
            h.WithOwner().HasForeignKey("surgery_id");
            h.HasKey("Id");
            h.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            h.Property(p => p.From).HasColumnName("from_status").HasNullableEnum();
            h.Property(p => p.To).HasColumnName("to_status").HasEnumerationValue().IsRequired();
            h.Property(p => p.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            h.Property(p => p.ChangedBy).HasColumnName("changed_by").IsRequired();
            h.Property(p => p.Note).HasColumnName("note").HasMaxLength(SurgeryRules.NoteMax);
            h.Property(p => p.Sequence).HasColumnName("sequence").IsRequired();
            h.HasIndex("surgery_id").HasDatabaseName("ix_surgery_recovery_change_surgery");
        });
        builder.Metadata.FindNavigation(nameof(SurgeryAggregate.RecoveryHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.BranchId, x.StartsOn }).HasDatabaseName("ix_surgery_branch_starts_on");
        builder.HasIndex(x => new { x.CatId, x.StartsOn }).HasDatabaseName("ix_surgery_cat_starts_on");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_surgery_customer");
        builder.HasIndex(x => new { x.Status, x.BranchId }).HasDatabaseName("ix_surgery_status_branch");
        builder.HasIndex(x => x.AppointmentId).IsUnique().HasDatabaseName("ix_surgery_appointment_id");
    }
}

internal static class SurgeryConfigurationExtensions
{
    /// <summary>An optional enumeration stored by its <c>Value</c> in a nullable integer column.</summary>
    public static PropertyBuilder<T?> HasNullableEnum<T>(this PropertyBuilder<T?> property) where T : Enumeration =>
        property.HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<T>(v.Value));
}
