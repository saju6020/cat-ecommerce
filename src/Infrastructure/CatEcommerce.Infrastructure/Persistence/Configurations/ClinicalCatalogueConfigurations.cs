using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-EMR-001 §8.2 / ADR-123 -- <c>clinic.diagnosis_code</c>; <c>code</c> UNIQUE. The one system code
/// (<c>SYS-NAD</c>) is seeded by the <c>AddClinicalCatalogues</c> migration.</summary>
public sealed class DiagnosisCodeConfiguration : IEntityTypeConfiguration<DiagnosisCodeAggregate>
{
    public void Configure(EntityTypeBuilder<DiagnosisCodeAggregate> builder)
    {
        builder.ToTable("diagnosis_code", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(ClinicalNoteRules.MaxDiagnosisCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(ClinicalNoteRules.MaxDiagnosisNameLength).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(ClinicalNoteRules.MaxCategoryLength);
        builder.Property(x => x.ExternalSystem).HasColumnName("external_system").HasMaxLength(ClinicalNoteRules.MaxExternalCodeLength);
        builder.Property(x => x.ExternalCode).HasColumnName("external_code").HasMaxLength(ClinicalNoteRules.MaxExternalCodeLength);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Ignore(x => x.IsSystem);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_diagnosis_code_code");
    }
}

/// <summary>HO-EMR-001 §8.5 -- <c>clinic.lab_analyte</c>; <c>code</c> UNIQUE. No seed (clinical content, OQ-3).</summary>
public sealed class LabAnalyteConfiguration : IEntityTypeConfiguration<LabAnalyteAggregate>
{
    public void Configure(EntityTypeBuilder<LabAnalyteAggregate> builder)
    {
        builder.ToTable("lab_analyte", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(DiagnosticRules.MaxAnalyteCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(ClinicalNoteRules.MaxCatalogueNameLength).IsRequired();
        builder.Property(x => x.ValueKind).HasColumnName("value_kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.DiagnosticKind).HasColumnName("diagnostic_kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(DiagnosticRules.MaxUnitLength);
        builder.Property(x => x.ReferenceLow).HasColumnName("reference_low").HasColumnType("numeric(14,4)");
        builder.Property(x => x.ReferenceHigh).HasColumnName("reference_high").HasColumnType("numeric(14,4)");
        builder.Property(x => x.NormalText).HasColumnName("normal_text").HasMaxLength(DiagnosticRules.MaxNormalTextLength);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_lab_analyte_code");
    }
}

/// <summary>HO-EMR-001 §8.4 / ADR-134 -- <c>clinic.note_template</c>; one ACTIVE template per service
/// (filtered unique index, the race backstop for the handler's check).</summary>
public sealed class NoteTemplateConfiguration : IEntityTypeConfiguration<NoteTemplateAggregate>
{
    public void Configure(EntityTypeBuilder<NoteTemplateAggregate> builder)
    {
        builder.ToTable("note_template", "clinic");
        builder.ConfigureAggregateBase();

        // Named ClinicServiceId on the aggregate: the platform Entity base already declares ServiceId.
        builder.Property(x => x.ClinicServiceId).HasColumnName("clinic_service_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(ClinicalNoteRules.MaxCatalogueNameLength).IsRequired();
        builder.Property(x => x.Subjective).HasColumnName("subjective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Objective).HasColumnName("objective").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Assessment).HasColumnName("assessment").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.Plan).HasColumnName("plan").HasMaxLength(ClinicalNoteRules.MaxSoapFieldLength).IsRequired();
        builder.Property(x => x.DischargeInstructions).HasColumnName("discharge_instructions").HasMaxLength(ClinicalNoteRules.MaxDischargeInstructionsLength).IsRequired();
        builder.Property(x => x.TemplateVersion).HasColumnName("template_version").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.ClinicServiceId).IsUnique().HasFilter("is_active").HasDatabaseName("ix_note_template_service_active");
    }
}
