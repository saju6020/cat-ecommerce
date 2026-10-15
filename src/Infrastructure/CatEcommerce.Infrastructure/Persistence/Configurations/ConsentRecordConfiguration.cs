using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-EMR-001 §8.9 / §12.1 / ADR-132 -- <c>clinic.consent_record</c>: a signed consent is a <b>Regulatory</b> record
/// (NFR-LEG-4): never deleted (a trigger refuses it), withdrawn by a status change that keeps who, when and why. An on-screen
/// signature is stroke data in a <c>jsonb</c> column -- numbers, not an image -- and a signed upload is a reference to a
/// stored file, unique where present. The exact consent-form version shown is stored (<c>policy_key</c>,
/// <c>policy_version_number</c>), so the wording signed is always retrievable.
/// </summary>
public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecordAggregate>
{
    public void Configure(EntityTypeBuilder<ConsentRecordAggregate> builder)
    {
        builder.ToTable("consent_record", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.AppointmentId).HasColumnName("appointment_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ProcedureDescription).HasColumnName("procedure_description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").HasEnumerationValue().IsRequired();

        builder.Property(x => x.Signature).HasColumnName("signature").HasColumnType("jsonb")
            .HasConversion(
                signature => signature == null ? null : signature.ToJson(),
                json => json == null ? null : SignatureStrokes.FromJson(json));

        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.SignedByName).HasColumnName("signed_by_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SignerRelationship).HasColumnName("signer_relationship").HasEnumerationValue().IsRequired();
        builder.Property(x => x.SignedByCustomerId).HasColumnName("signed_by_customer_id");
        builder.Property(x => x.SignedOn).HasColumnName("signed_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.PolicyKey).HasColumnName("policy_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.PolicyVersionNumber).HasColumnName("policy_version_number").IsRequired();
        builder.Property(x => x.CapturedBy).HasColumnName("captured_by").IsRequired();
        builder.Property(x => x.WitnessStaffId).HasColumnName("witness_staff_id");
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.WithdrawnOn).HasColumnName("withdrawn_on").HasColumnType("timestamptz");
        builder.Property(x => x.WithdrawnBy).HasColumnName("withdrawn_by");
        builder.Property(x => x.WithdrawalReason).HasColumnName("withdrawal_reason").HasMaxLength(500);

        // The consent-status contract Surgery reads (§8.9).
        builder.HasIndex(x => new { x.CatId, x.AppointmentId, x.Kind, x.Status }).HasDatabaseName("ix_consent_record_cat_appointment_kind_status");

        // A signed file is attached once (§12.1, FileAlreadyAttached).
        builder.HasIndex(x => x.FileId).IsUnique().HasFilter("file_id IS NOT NULL").HasDatabaseName("ix_consent_record_file_id");
    }
}
