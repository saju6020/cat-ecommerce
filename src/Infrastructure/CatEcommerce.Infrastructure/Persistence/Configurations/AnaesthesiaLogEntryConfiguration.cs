using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-SRG-001 §8.8 / §12.1 (ADR-162) -- <c>clinic.anaesthesia_log_entry</c>: append-only. A row is inserted once and never updated, so a <c>BEFORE UPDATE OR DELETE</c> trigger
/// (<c>trg_anaesthesia_log_entry_immutable</c>, hand-written in the <c>AddAnaesthesiaLog</c> migration) can refuse any change. Doses and quantities are <c>numeric</c>; the temperature is
/// <c>numeric(4,1)</c>. No foreign key to the surgery, the medicine, the batch or the register entry (ADR-011) -- the ids are the link, and the register and the ledger own their rows.
/// </summary>
public sealed class AnaesthesiaLogEntryConfiguration : IEntityTypeConfiguration<AnaesthesiaLogEntryAggregate>
{
    public void Configure(EntityTypeBuilder<AnaesthesiaLogEntryAggregate> builder)
    {
        builder.ToTable("anaesthesia_log_entry", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.SurgeryId).HasColumnName("surgery_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.LoggedBy).HasColumnName("logged_by").IsRequired();
        builder.Property(x => x.LoggedOn).HasColumnName("logged_on").HasColumnType("timestamptz").IsRequired();

        builder.Property(x => x.MedicineId).HasColumnName("medicine_id");
        builder.Property(x => x.MedicineName).HasColumnName("medicine_name").HasMaxLength(200);
        builder.Property(x => x.IsControlled).HasColumnName("is_controlled").IsRequired();
        builder.Property(x => x.DoseAmount).HasColumnName("dose_amount").HasColumnType("numeric(10,3)");
        builder.Property(x => x.DoseUnit).HasColumnName("dose_unit").HasNullableEnum();
        builder.Property(x => x.Route).HasColumnName("route").HasNullableEnum();
        builder.Property(x => x.AdministeredById).HasColumnName("administered_by");
        builder.Property(x => x.SkuId).HasColumnName("sku_id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.BatchNumber).HasColumnName("batch_number").HasMaxLength(60);
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.QuantityDrawn).HasColumnName("quantity_drawn").HasColumnType("numeric(12,3)");
        builder.Property(x => x.QuantityWasted).HasColumnName("quantity_wasted").HasColumnType("numeric(12,3)");
        builder.Property(x => x.StockUnit).HasColumnName("stock_unit").HasMaxLength(30);
        builder.Property(x => x.WitnessId).HasColumnName("witness_id");
        builder.Property(x => x.RegisterEntryId).HasColumnName("register_entry_id");
        builder.Property(x => x.StockShortfall).HasColumnName("stock_shortfall").IsRequired();

        builder.Property(x => x.HeartRateBpm).HasColumnName("heart_rate_bpm");
        builder.Property(x => x.RespiratoryRatePerMin).HasColumnName("respiratory_rate_per_min");
        builder.Property(x => x.SpO2Percent).HasColumnName("spo2_percent");
        builder.Property(x => x.EtCo2MmHg).HasColumnName("etco2_mmhg");
        builder.Property(x => x.TemperatureCelsius).HasColumnName("temperature_celsius").HasColumnType("numeric(4,1)");
        builder.Property(x => x.SystolicMmHg).HasColumnName("systolic_mmhg");
        builder.Property(x => x.MeanArterialMmHg).HasColumnName("mean_arterial_mmhg");
        builder.Property(x => x.ImplausibleConfirmed).HasColumnName("implausible_confirmed").IsRequired();

        builder.Property(x => x.Milestone).HasColumnName("milestone").HasNullableEnum();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(SurgeryRules.NoteMax);

        builder.Property(x => x.CorrectsEntryId).HasColumnName("corrects_entry_id");
        builder.Property(x => x.CorrectedKind).HasColumnName("corrected_kind").HasNullableEnum();
        builder.Property(x => x.CorrectionReason).HasColumnName("correction_reason").HasMaxLength(SurgeryRules.CorrectionReasonMax);

        builder.HasIndex(x => new { x.SurgeryId, x.OccurredOn, x.LoggedOn }).HasDatabaseName("ix_anaesthesia_log_entry_timeline");
        // UNIQUE: one correction per entry, so a chain never forks (two simultaneous corrections of one entry: the second is refused with 409 by UniqueConstraintExceptionFilter).
        builder.HasIndex(x => x.CorrectsEntryId).IsUnique().HasDatabaseName("ix_anaesthesia_log_entry_corrects").HasFilter("corrects_entry_id IS NOT NULL");
    }
}
