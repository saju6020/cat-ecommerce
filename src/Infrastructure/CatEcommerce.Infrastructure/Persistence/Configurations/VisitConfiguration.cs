using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-EMR-001 §8.2 / §12.1 -- <c>clinic.visit</c> and its owned collections (<c>visit_problem</c>,
/// <c>visit_vitals</c>, <c>visit_photo</c>, <c>visit_treatment</c>); the SOAP note and the follow-up are columns on <c>visit</c>.
///
/// There is deliberately <b>no customer id</b> on any of these tables (§8.2). A frozen row cannot follow
/// a merge, and a customer id in a clinical row would pull the table into the personal-data eraser's
/// scope (ErasureExemptions.Consultation).
///
/// The database-layer immutability triggers (ADR-118) are raw SQL in <c>AddClinicalRecordImmutability</c>
/// and refer to the column names fixed here: <c>visit.status</c>, <c>visit_*.visit_id</c>.
/// </summary>
public sealed class VisitConfiguration : IEntityTypeConfiguration<VisitAggregate>
{
    public void Configure(EntityTypeBuilder<VisitAggregate> builder)
    {
        builder.ToTable("visit", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Origin).HasColumnName("origin").HasEnumerationValue().IsRequired();
        builder.Property(x => x.AppointmentId).HasColumnName("appointment_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();

        // The platform Entity base already declares ServiceId (an optional text), so this is ClinicServiceId.
        builder.Property(x => x.ClinicServiceId).HasColumnName("clinic_service_id").IsRequired();
        builder.Property(x => x.ServiceName).HasColumnName("service_name").HasMaxLength(ClinicalNoteRules.MaxServiceNameLength).IsRequired();
        builder.Property(x => x.ServiceCategory).HasColumnName("service_category").HasEnumerationValue().IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.OpenedOn).HasColumnName("opened_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.OpenedBy).HasColumnName("opened_by").IsRequired();
        builder.Property(x => x.FinalisedOn).HasColumnName("finalised_on").HasColumnType("timestamptz");
        builder.Property(x => x.FinalisedBy).HasColumnName("finalised_by");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id"); // HO-BIL-001 Phase J: the cat's owner at finalising; null before Phase J

        builder.OwnsOne(x => x.Note, note =>
        {
            note.Property(p => p.Subjective).HasColumnName("subjective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
            note.Property(p => p.Objective).HasColumnName("objective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
            note.Property(p => p.Assessment).HasColumnName("assessment").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
            note.Property(p => p.Plan).HasColumnName("plan").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
            note.Property(p => p.DischargeInstructions).HasColumnName("discharge_instructions").HasMaxLength(ClinicalNoteRules.MaxDischargeInstructionsLength).IsRequired();
            note.Property(p => p.NoteSavedOn).HasColumnName("note_saved_on").HasColumnType("timestamptz");
            note.Property(p => p.NoteSavedBy).HasColumnName("note_saved_by");
            note.Property(p => p.AppliedTemplateId).HasColumnName("applied_template_id");
            note.Property(p => p.AppliedTemplateVersion).HasColumnName("applied_template_version");
            note.Ignore(p => p.HasBeenSaved);
        });
        builder.Navigation(x => x.Note).IsRequired();

        builder.OwnsOne(x => x.FollowUp, followUp =>
        {
            followUp.Property(p => p.Kind).HasColumnName("follow_up_kind").HasEnumerationValue().IsRequired();
            followUp.Property(p => p.DueOn).HasColumnName("follow_up_due_on").HasColumnType("date").IsRequired();
            followUp.Property(p => p.AppointmentId).HasColumnName("follow_up_appointment_id");
            followUp.Property(p => p.Stamp).HasColumnName("follow_up_stamp").HasMaxLength(64).IsRequired();
            followUp.Property(p => p.SetOn).HasColumnName("follow_up_set_on").HasColumnType("timestamptz").IsRequired();
            followUp.Property(p => p.SetBy).HasColumnName("follow_up_set_by").IsRequired();
        });

        ConfigureProblems(builder);
        ConfigureVitals(builder);
        ConfigurePhotos(builder);
        ConfigureTreatments(builder);

        builder.Ignore(x => x.ActiveProblems);

        // §12.1 -- one visit per appointment; the unique index turns a race into the same answer (C5).
        builder.HasIndex(x => x.AppointmentId).IsUnique().HasDatabaseName("ix_visit_appointment");
        builder.HasIndex(x => new { x.CatId, x.OpenedOn }).IsDescending(false, true).HasDatabaseName("ix_visit_cat_opened_on");
        builder.HasIndex(x => new { x.BranchId, x.Status, x.OpenedOn }).HasDatabaseName("ix_visit_branch_status_opened_on");
    }

    private static void ConfigureProblems(EntityTypeBuilder<VisitAggregate> builder)
    {
        builder.OwnsMany(x => x.Problems, problem =>
        {
            problem.ToTable("visit_problem", "clinic");
            problem.WithOwner().HasForeignKey("visit_id");
            problem.HasKey("Id");
            problem.Property(p => p.Id).ValueGeneratedNever();
            problem.Property(p => p.DiagnosisCodeId).HasColumnName("diagnosis_code_id").IsRequired();
            problem.Property(p => p.Code).HasColumnName("code").HasMaxLength(ClinicalNoteRules.MaxDiagnosisCodeLength).IsRequired();
            problem.Property(p => p.Name).HasColumnName("name").HasMaxLength(ClinicalNoteRules.MaxDiagnosisNameLength).IsRequired();
            problem.Property(p => p.Certainty).HasColumnName("certainty").HasEnumerationValue().IsRequired();
            problem.Property(p => p.Note).HasColumnName("note").HasMaxLength(ClinicalNoteRules.MaxProblemNoteLength);
            problem.Property(p => p.AddedOn).HasColumnName("added_on").HasColumnType("timestamptz").IsRequired();
            problem.Property(p => p.WithdrawnOn).HasColumnName("withdrawn_on").HasColumnType("timestamptz");
            problem.Ignore(p => p.IsActive);

            problem.HasIndex("visit_id").HasDatabaseName("ix_visit_problem_visit_id");
            problem.HasIndex(p => p.DiagnosisCodeId).HasDatabaseName("ix_visit_problem_diagnosis_code_id");
        });
        builder.Navigation(x => x.Problems).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureVitals(EntityTypeBuilder<VisitAggregate> builder)
    {
        builder.OwnsMany(x => x.Vitals, vitals =>
        {
            vitals.ToTable("visit_vitals", "clinic");
            vitals.WithOwner().HasForeignKey("visit_id");
            vitals.HasKey("Id");
            vitals.Property(p => p.Id).ValueGeneratedNever(); // also the weight record's id (ADR-121)
            vitals.Property(p => p.MeasuredOn).HasColumnName("measured_on").HasColumnType("timestamptz").IsRequired();
            vitals.Property(p => p.WeightKg).HasColumnName("weight_kg").HasColumnType("numeric(5,2)");
            vitals.Property(p => p.TemperatureC).HasColumnName("temperature_c").HasColumnType("numeric(4,1)");
            vitals.Property(p => p.HeartRateBpm).HasColumnName("heart_rate_bpm");
            vitals.Property(p => p.RespiratoryRatePerMin).HasColumnName("respiratory_rate_per_min");
            vitals.Property(p => p.BodyConditionScore).HasColumnName("body_condition_score");
            vitals.Property(p => p.PainScore).HasColumnName("pain_score");
            vitals.Property(p => p.ConfirmedOutOfRange).HasColumnName("confirmed_out_of_range").HasColumnType("integer[]").IsRequired();
            vitals.Property(p => p.Note).HasColumnName("note").HasMaxLength(ClinicalNoteRules.MaxVitalsNoteLength);
            vitals.Property(p => p.RecordedBy).HasColumnName("recorded_by").IsRequired();
            vitals.Property(p => p.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();

            vitals.HasIndex("visit_id").HasDatabaseName("ix_visit_vitals_visit_id");
        });
        builder.Navigation(x => x.Vitals).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurePhotos(EntityTypeBuilder<VisitAggregate> builder)
    {
        builder.OwnsMany(x => x.Photos, photo =>
        {
            photo.ToTable("visit_photo", "clinic");
            photo.WithOwner().HasForeignKey("visit_id");
            photo.HasKey("Id");
            photo.Property(p => p.Id).ValueGeneratedNever();
            photo.Property(p => p.FileId).HasColumnName("file_id").IsRequired();
            photo.Property(p => p.TakenOn).HasColumnName("taken_on").HasColumnType("date").IsRequired();
            photo.Property(p => p.Note).HasColumnName("note").HasMaxLength(ClinicalNoteRules.MaxPhotoNoteLength);
            photo.Property(p => p.AddedOn).HasColumnName("added_on").HasColumnType("timestamptz").IsRequired();
            photo.Property(p => p.AddedBy).HasColumnName("added_by").IsRequired();

            photo.HasIndex("visit_id").HasDatabaseName("ix_visit_photo_visit_id");
            // A file is attached once (§12.1, FileAlreadyAttached).
            photo.HasIndex(p => p.FileId).IsUnique().HasDatabaseName("ix_visit_photo_file_id");
        });
        builder.Navigation(x => x.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureTreatments(EntityTypeBuilder<VisitAggregate> builder)
    {
        builder.OwnsMany(x => x.Treatments, line =>
        {
            line.ToTable("visit_treatment", "clinic");
            line.WithOwner().HasForeignKey("visit_id");
            line.HasKey("Id");
            line.Property(p => p.Id).ValueGeneratedNever();
            line.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            line.Property(p => p.Description).HasColumnName("description").HasMaxLength(ClinicalNoteRules.MaxTreatmentDescriptionLength).IsRequired();
            line.Property(p => p.ClinicServiceId).HasColumnName("clinic_service_id");
            line.Property(p => p.SkuId).HasColumnName("sku_id");
            line.Property(p => p.SkuName).HasColumnName("sku_name").HasMaxLength(300);
            line.Property(p => p.StockUnit).HasColumnName("stock_unit").HasMaxLength(30);
            line.Property(p => p.BatchId).HasColumnName("batch_id");
            line.Property(p => p.LotNumber).HasColumnName("lot_number").HasMaxLength(100);
            line.Property(p => p.BatchExpiresOn).HasColumnName("batch_expires_on").HasColumnType("date");
            line.Property(p => p.LocationId).HasColumnName("location_id");
            line.Property(p => p.Quantity).HasColumnName("quantity").HasColumnType("numeric(12,3)").IsRequired();
            line.Property(p => p.DeductedQuantity).HasColumnName("deducted_quantity").HasColumnType("numeric(12,3)").IsRequired();
            line.Property(p => p.ShortfallQuantity).HasColumnName("shortfall_quantity").HasColumnType("numeric(12,3)").IsRequired();
            line.Property(p => p.Dose).HasColumnName("dose").HasMaxLength(ClinicalNoteRules.MaxDoseLength);
            line.Property(p => p.Route).HasColumnName("route").HasMaxLength(ClinicalNoteRules.MaxRouteLength);
            line.Property(p => p.IsBillable).HasColumnName("is_billable").IsRequired();
            line.Property(p => p.UnitPriceAmount).HasColumnName("unit_price_amount").HasColumnType("numeric(12,2)");
            line.Property(p => p.UnitPriceCurrency).HasColumnName("unit_price_currency_code").HasColumnType("char(3)");
            line.Property(p => p.TaxClassId).HasColumnName("tax_class_id");
            line.Property(p => p.BatchOverrideReason).HasColumnName("batch_override_reason").HasMaxLength(ClinicalNoteRules.MaxBatchOverrideReasonLength);
            line.Property(p => p.Position).HasColumnName("position").IsRequired();
            line.Property(p => p.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
            line.Property(p => p.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
            line.Property(p => p.RecordedBy).HasColumnName("recorded_by").IsRequired();
            line.Property(p => p.ReversedOn).HasColumnName("reversed_on").HasColumnType("timestamptz");
            line.Property(p => p.ReversedBy).HasColumnName("reversed_by");
            line.Property(p => p.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(ClinicalNoteRules.MaxTreatmentReversalReasonLength);
            line.Ignore(p => p.UnitPrice);
            line.Ignore(p => p.IsReversed);

            line.HasIndex("visit_id").HasDatabaseName("ix_visit_treatment_visit_id");
            // Recall tracing (HO-IMP-001) and the charge-lines read (section 12.1).
            line.HasIndex(p => p.BatchId).HasDatabaseName("ix_visit_treatment_batch_id");
            line.HasIndex(p => p.SkuId).HasDatabaseName("ix_visit_treatment_sku_id");
        });
        builder.Navigation(x => x.Treatments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
