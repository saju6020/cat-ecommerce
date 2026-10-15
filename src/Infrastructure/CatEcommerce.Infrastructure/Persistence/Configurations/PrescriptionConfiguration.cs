using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-PHM-001 §8.3 / §12.1 -- <c>pharmacy.prescription</c> and its owned children (<c>prescription_warning</c>, <c>repeat_request</c>). The medicine snapshot,
/// the dose and the safety record are columns of the prescription. Quantities are <c>numeric(10,2)</c>, strengths and mg/kg <c>numeric(12,4)</c>, instants
/// <c>timestamptz</c>, dates <c>date</c>. There is NO foreign key to the visit, the cat or the medicine (ADR-011, ADR-151: references by id across contexts).
/// Every id, owned ones included, is <c>ValueGeneratedNever()</c> (ADR-021).
/// </summary>
public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<PrescriptionAggregate>
{
    public void Configure(EntityTypeBuilder<PrescriptionAggregate> builder)
    {
        builder.ToTable("prescription", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.PrescriptionNumber).HasColumnName("prescription_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.IsRemoteRenewal).HasColumnName("is_remote_renewal").IsRequired();
        builder.Property(x => x.PrescriberId).HasColumnName("prescriber_id").IsRequired();
        builder.Property(x => x.MedicineId).HasColumnName("medicine_id").IsRequired();
        builder.Property(x => x.Route).HasColumnName("route").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Frequency).HasColumnName("frequency").HasEnumerationValue().IsRequired();
        builder.Property(x => x.DurationDays).HasColumnName("duration_days").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(x => x.Instructions).HasColumnName("instructions").HasMaxLength(PrescriptionRules.MaxInstructionsLength).IsRequired();
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.IsRepeatable).HasColumnName("is_repeatable").IsRequired();
        builder.Property(x => x.RepeatsAuthorised).HasColumnName("repeats_authorised").IsRequired();
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until").HasColumnType("date").IsRequired();
        builder.Property(x => x.SuppliesDispensed).HasColumnName("supplies_dispensed").IsRequired();
        builder.Property(x => x.LastDispensedOn).HasColumnName("last_dispensed_on").HasColumnType("date");
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(PrescriptionRules.MaxReasonLength);
        builder.Property(x => x.RenewedFromPrescriptionId).HasColumnName("renewed_from_prescription_id");
        builder.Property(x => x.RenewedByPrescriptionId).HasColumnName("renewed_by_prescription_id");

        builder.OwnsOne(x => x.MedicineSnapshot, medicine =>
        {
            medicine.Property(p => p.Code).HasColumnName("medicine_code").HasMaxLength(FormularyRules.MaxCodeLength).IsRequired();
            medicine.Property(p => p.Name).HasColumnName("medicine_name").HasMaxLength(FormularyRules.MaxNameLength).IsRequired();
            medicine.Property(p => p.Form).HasColumnName("medicine_form").HasEnumerationValue().IsRequired();
            medicine.Property(p => p.StrengthText).HasColumnName("medicine_strength_text").HasMaxLength(100).IsRequired();
            medicine.Property(p => p.IsControlled).HasColumnName("medicine_is_controlled").IsRequired();
            medicine.Property(p => p.ControlledSchedule).HasColumnName("medicine_controlled_schedule").HasMaxLength(ControlledDrugRules.MaxScheduleLength);
            medicine.Property(p => p.DispensingUnit).HasColumnName("medicine_dispensing_unit").HasEnumerationValue().IsRequired();
            medicine.Property(p => p.LinkedSkuIds).HasColumnName("medicine_linked_sku_ids").HasColumnType("uuid[]").IsRequired();
        });
        builder.Navigation(x => x.MedicineSnapshot).IsRequired();

        builder.OwnsOne(x => x.Dose, dose =>
        {
            dose.Property(p => p.Amount).HasColumnName("dose_amount").HasColumnType("numeric(12,4)").IsRequired();
            dose.Property(p => p.Unit).HasColumnName("dose_unit").HasEnumerationValue().IsRequired();
            dose.Ignore(p => p.AmountText);
        });
        builder.Navigation(x => x.Dose).IsRequired();

        builder.OwnsOne(x => x.SafetyRecord, safety =>
        {
            safety.Property(p => p.WeightKg).HasColumnName("safety_weight_kg").HasColumnType("numeric(6,2)");
            safety.Property(p => p.WeightRecordedOn).HasColumnName("safety_weight_recorded_on").HasColumnType("date");
            safety.Property(p => p.DoseMgPerKg).HasColumnName("safety_dose_mg_per_kg").HasColumnType("numeric(12,4)");
            safety.Property(p => p.ExpectedMinMgPerKg).HasColumnName("safety_expected_min_mg_per_kg").HasColumnType("numeric(12,4)");
            safety.Property(p => p.ExpectedMaxMgPerKg).HasColumnName("safety_expected_max_mg_per_kg").HasColumnType("numeric(12,4)");
            safety.Property(p => p.DoseCheckOutcome).HasColumnName("safety_dose_check_outcome").HasEnumerationValue().IsRequired();
            safety.Property(p => p.WarningsFingerprint).HasColumnName("safety_warnings_fingerprint").HasMaxLength(64).IsRequired();
            safety.Property(p => p.AcknowledgedBy).HasColumnName("safety_acknowledged_by");
            safety.Property(p => p.AcknowledgedOn).HasColumnName("safety_acknowledged_on").HasColumnType("timestamptz");
            safety.Property(p => p.AllergyAcknowledgementReason).HasColumnName("safety_allergy_ack_reason").HasMaxLength(PrescriptionRules.MaxNoteLength);
            safety.Property(p => p.DoseAcknowledgementNote).HasColumnName("safety_dose_ack_note").HasMaxLength(PrescriptionRules.MaxNoteLength);
        });
        builder.Navigation(x => x.SafetyRecord).IsRequired();

        builder.OwnsMany(x => x.Warnings, warning =>
        {
            warning.ToTable("prescription_warning", "pharmacy");
            warning.WithOwner().HasForeignKey("prescription_id");
            warning.HasKey("Id");
            warning.Property(p => p.Id).ValueGeneratedNever();
            warning.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            warning.Property(p => p.Blocking).HasColumnName("blocking").IsRequired();
            warning.Property(p => p.RequiresAcknowledgement).HasColumnName("requires_acknowledgement").IsRequired();
            warning.Property(p => p.Severity).HasColumnName("severity")
                .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<InteractionSeverity>(v.Value));
            warning.Property(p => p.Subject).HasColumnName("subject").HasMaxLength(PrescriptionRules.MaxWarningSubjectLength).IsRequired();
            warning.Property(p => p.Detail).HasColumnName("detail").HasMaxLength(PrescriptionRules.MaxWarningDetailLength).IsRequired();
            warning.HasIndex("prescription_id").HasDatabaseName("ix_prescription_warning_prescription");
        });
        builder.Navigation(x => x.Warnings).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.RepeatRequests, request =>
        {
            request.ToTable("repeat_request", "pharmacy");
            request.WithOwner().HasForeignKey("prescription_id");
            request.HasKey("Id");
            request.Property(p => p.Id).ValueGeneratedNever();
            request.Property(p => p.RequestedBy).HasColumnName("requested_by").IsRequired();
            request.Property(p => p.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
            request.Property(p => p.PreferredBranchId).HasColumnName("preferred_branch_id").IsRequired();
            request.Property(p => p.Note).HasColumnName("note").HasMaxLength(RepeatRules.MaxNoteLength);
            request.Property(p => p.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
            request.Property(p => p.DecidedBy).HasColumnName("decided_by");
            request.Property(p => p.DecidedOn).HasColumnName("decided_on").HasColumnType("timestamptz");
            request.Property(p => p.DeclineReason).HasColumnName("decline_reason").HasMaxLength(RepeatRules.MaxDeclineReasonLength);
            request.Property(p => p.DispensingId).HasColumnName("dispensing_id");
            request.Ignore(p => p.IsOpen);
            request.HasIndex("prescription_id").HasDatabaseName("ix_repeat_request_prescription");
            request.HasIndex(p => new { p.Status, p.PreferredBranchId }).HasDatabaseName("ix_repeat_request_status_branch");
        });
        builder.Navigation(x => x.RepeatRequests).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.SuppliesLeft);
        builder.Ignore(x => x.CourseEndsOn);
        builder.Ignore(x => x.OpenRepeatRequest);

        builder.HasIndex(x => x.PrescriptionNumber).IsUnique().HasDatabaseName("ix_prescription_number");
        builder.HasIndex(x => new { x.CatId, x.Status }).HasDatabaseName("ix_prescription_cat_status");
        builder.HasIndex(x => x.VisitId).HasDatabaseName("ix_prescription_visit");
        builder.HasIndex(x => new { x.BranchId, x.Status, x.IssuedOn }).HasDatabaseName("ix_prescription_branch_status_issued_on");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_prescription_customer");
        builder.HasIndex(x => new { x.Status, x.ValidUntil }).HasDatabaseName("ix_prescription_status_valid_until");
    }
}
