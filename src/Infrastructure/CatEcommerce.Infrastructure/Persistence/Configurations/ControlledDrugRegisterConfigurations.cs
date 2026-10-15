using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-PHM-001 §8.9 / §12.1 (ADR-144) -- <c>pharmacy.cd_register_entry</c>, the controlled-drug register. <b>Append-only at three layers</b>: the aggregate
/// has no mutator, <c>ControlledDrugRegisterImmutabilityInterceptor</c> refuses a modified or deleted entry before SQL is sent, and PostgreSQL triggers
/// (<c>ControlledDrugRegisterSql</c>, migration <c>AddControlledDrugRegister</c>) refuse UPDATE, DELETE and TRUNCATE. <c>entry_number</c> is a
/// <c>bigint</c> filled from <c>pharmacy.cd_register_entry_number_seq</c> by the column default (V1) -- EF reads it back and never writes it. There is no
/// foreign key to the batch, the SKU, the cat or the staff member (ADR-011, ADR-151): references by id, so the register stays readable whatever happens to
/// the rows it points at. The medicine's name, strength, form, schedule and unit are frozen on the entry itself.
/// </summary>
public sealed class ControlledDrugRegisterEntryConfiguration : IEntityTypeConfiguration<ControlledDrugRegisterEntryAggregate>
{
    public void Configure(EntityTypeBuilder<ControlledDrugRegisterEntryAggregate> builder)
    {
        builder.ToTable("cd_register_entry", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.EntryNumber).HasColumnName("entry_number").HasDefaultValueSql("nextval('pharmacy.cd_register_entry_number_seq')").ValueGeneratedOnAdd();
        builder.Property(x => x.EntryNumber).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(x => x.EntryNumber).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.MedicineId).HasColumnName("medicine_id").IsRequired();
        builder.Property(x => x.MedicineName).HasColumnName("medicine_name").HasMaxLength(FormularyRules.MaxNameLength).IsRequired();
        builder.Property(x => x.MedicineStrengthText).HasColumnName("medicine_strength_text").HasMaxLength(100).IsRequired();
        builder.Property(x => x.MedicineForm).HasColumnName("medicine_form").HasEnumerationValue().IsRequired();
        builder.Property(x => x.MedicineControlledSchedule).HasColumnName("medicine_controlled_schedule").HasMaxLength(ControlledDrugRules.MaxScheduleLength);
        builder.Property(x => x.MedicineDispensingUnit).HasColumnName("medicine_dispensing_unit").HasEnumerationValue().IsRequired();
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.LotNumber).HasColumnName("lot_number").HasMaxLength(60);
        builder.Property(x => x.BatchExpiresOn).HasColumnName("batch_expires_on").HasColumnType("date");
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.QuantityDelta).HasColumnName("quantity_delta").HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(x => x.DoseText).HasColumnName("dose_text").HasMaxLength(ControlledDrugRules.MaxDoseTextLength);
        builder.Property(x => x.WastageQuantity).HasColumnName("wastage_quantity").HasColumnType("numeric(10,2)");
        builder.Property(x => x.CatId).HasColumnName("cat_id");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(x => x.WitnessId).HasColumnName("witness_id");
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasEnumerationValue().IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(x => x.CorrectsEntryId).HasColumnName("corrects_entry_id");
        builder.Property(x => x.CorrectionReason).HasColumnName("correction_reason").HasMaxLength(ControlledDrugRules.MaxReasonLength);
        builder.Property(x => x.ReturnedEntryId).HasColumnName("returned_entry_id");
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(ControlledDrugRules.MaxNotesLength);

        builder.HasIndex(x => x.EntryNumber).IsUnique().HasDatabaseName("ix_cd_register_entry_number");
        builder.HasIndex(x => new { x.BranchId, x.MedicineId, x.EntryNumber }).HasDatabaseName("ix_cd_register_entry_branch_medicine");
        builder.HasIndex(x => new { x.BranchId, x.BatchId }).HasDatabaseName("ix_cd_register_entry_branch_batch");
        builder.HasIndex(x => new { x.SourceType, x.SourceId }).HasDatabaseName("ix_cd_register_entry_source");
        builder.HasIndex(x => x.CorrectsEntryId).HasDatabaseName("ix_cd_register_entry_corrects").HasFilter("corrects_entry_id IS NOT NULL");
        builder.HasIndex(x => new { x.BranchId, x.RecordedOn }).HasDatabaseName("ix_cd_register_entry_branch_recorded_on");
    }
}

/// <summary>HO-PHM-001 §8.9 / §12.1 (ADR-145) -- <c>pharmacy.cd_register_seal</c>, the per-branch hash chain over the register. The primary key IS the entry id.
/// Append-only exactly like the entries; <c>(branch_id, chain_position)</c> is unique so two workers racing produce a <c>23505</c> and one stops.</summary>
public sealed class ControlledDrugRegisterSealConfiguration : IEntityTypeConfiguration<ControlledDrugRegisterSealAggregate>
{
    public void Configure(EntityTypeBuilder<ControlledDrugRegisterSealAggregate> builder)
    {
        builder.ToTable("cd_register_seal", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.ChainPosition).HasColumnName("chain_position").IsRequired();
        builder.Property(x => x.PreviousHash).HasColumnName("previous_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.EntryHash).HasColumnName("entry_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.SealedOn).HasColumnName("sealed_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.ChainPosition }).IsUnique().HasDatabaseName("ix_cd_register_seal_chain");
    }
}
