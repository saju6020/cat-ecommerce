using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-PHM-001 §8.7 / §12.1 (ADR-143, ADR-147) -- <c>pharmacy.dispensing</c> and its owned lines (<c>dispensing_line</c>). The medicine label snapshot is columns of the
/// dispensing, frozen at dispensing. A supply number is used once among the supplies that stand: <c>ix_dispensing_supply</c> is UNIQUE on
/// <c>(prescription_id, supply_number)</c> filtered to <c>Dispensed</c> status (written from the enumeration, never a literal -- U-X3), so a reversed supply frees its number
/// for the next one while two concurrent dispensers of the same supply still collide. No foreign key to the prescription, batch or SKU (ADR-011, ADR-151).
/// </summary>
public sealed class DispensingConfiguration : IEntityTypeConfiguration<DispensingAggregate>
{
    public void Configure(EntityTypeBuilder<DispensingAggregate> builder)
    {
        builder.ToTable("dispensing", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.PrescriptionId).HasColumnName("prescription_id").IsRequired();
        builder.Property(x => x.PrescriptionNumber).HasColumnName("prescription_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.SupplyNumber).HasColumnName("supply_number").IsRequired();
        builder.Property(x => x.IsRepeat).HasColumnName("is_repeat").IsRequired();
        builder.Property(x => x.RepeatRequestId).HasColumnName("repeat_request_id");
        builder.Property(x => x.DispensedBy).HasColumnName("dispensed_by").IsRequired();
        builder.Property(x => x.DispensedOn).HasColumnName("dispensed_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.DispensedOnLocal).HasColumnName("dispensed_on_local").HasColumnType("date").IsRequired();
        builder.Property(x => x.WitnessId).HasColumnName("witness_id");
        builder.Property(x => x.FefoOverrideReason).HasColumnName("fefo_override_reason").HasMaxLength(DispensingRules.MaxReasonLength);
        builder.Property(x => x.LabelPrintCount).HasColumnName("label_print_count").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ReversedOn).HasColumnName("reversed_on").HasColumnType("timestamptz");
        builder.Property(x => x.ReversedBy).HasColumnName("reversed_by");
        builder.Property(x => x.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(DispensingRules.MaxReasonLength);

        builder.OwnsOne(x => x.Label, label =>
        {
            label.Property(p => p.CatName).HasColumnName("label_cat_name").HasMaxLength(200).IsRequired();
            label.Property(p => p.OwnerName).HasColumnName("label_owner_name").HasMaxLength(200).IsRequired();
            label.Property(p => p.MedicineLine).HasColumnName("label_medicine_line").HasMaxLength(400).IsRequired();
            label.Property(p => p.DoseLine).HasColumnName("label_dose_line").HasMaxLength(400).IsRequired();
            label.Property(p => p.Instructions).HasColumnName("label_instructions").HasMaxLength(PrescriptionRules.MaxInstructionsLength).IsRequired();
            label.Property(p => p.DispensedOnLocal).HasColumnName("label_dispensed_on").HasColumnType("date").IsRequired();
            label.Property(p => p.ExpiresOn).HasColumnName("label_expires_on").HasColumnType("date");
            label.Property(p => p.QuantityLine).HasColumnName("label_quantity_line").HasMaxLength(100).IsRequired();
            label.Property(p => p.PrescriptionNumber).HasColumnName("label_prescription_number").HasMaxLength(20).IsRequired();
            label.Property(p => p.PrescriberName).HasColumnName("label_prescriber_name").HasMaxLength(200).IsRequired();
            label.Property(p => p.ClinicName).HasColumnName("label_clinic_name").HasMaxLength(200).IsRequired();
            label.Property(p => p.ClinicPhone).HasColumnName("label_clinic_phone").HasMaxLength(50).IsRequired();
            label.Property(p => p.ClinicAddress).HasColumnName("label_clinic_address").HasMaxLength(500);
            label.Property(p => p.ControlledNotice).HasColumnName("label_controlled_notice").HasMaxLength(100);
            label.Property(p => p.StandardWarnings).HasColumnName("label_standard_warnings").HasMaxLength(500).IsRequired();
        });
        builder.Navigation(x => x.Label).IsRequired();

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("dispensing_line", "pharmacy");
            line.WithOwner().HasForeignKey("dispensing_id");
            line.HasKey("Id");
            line.Property(p => p.Id).ValueGeneratedNever();
            line.Property(p => p.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(p => p.BatchId).HasColumnName("batch_id").IsRequired();
            line.Property(p => p.LotNumber).HasColumnName("lot_number").HasMaxLength(60).IsRequired();
            line.Property(p => p.ExpiresOn).HasColumnName("expires_on").HasColumnType("date");
            line.Property(p => p.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,2)").IsRequired();
            line.Property(p => p.RegisterEntryId).HasColumnName("register_entry_id");
            line.OwnsOne(p => p.PriceSnapshot, price =>
            {
                price.Property(p => p.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)");
                price.Property(p => p.LineTotal).HasColumnName("line_total").HasColumnType("numeric(12,2)");
                price.Property(p => p.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)");
            });
            line.HasIndex("dispensing_id").HasDatabaseName("ix_dispensing_line_dispensing");
            line.HasIndex(p => p.BatchId).HasDatabaseName("ix_dispensing_line_batch");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.PrescriptionId, x.SupplyNumber }).IsUnique().HasFilter($"status = {DispensingStatus.Dispensed.Value}").HasDatabaseName("ix_dispensing_supply");
        builder.HasIndex(x => new { x.BranchId, x.DispensedOn }).HasDatabaseName("ix_dispensing_branch_dispensed_on");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_dispensing_customer");
        builder.HasIndex(x => x.CatId).HasDatabaseName("ix_dispensing_cat");
    }
}
