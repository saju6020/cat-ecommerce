using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-PHM-001 §8.2 / §12.1 -- <c>pharmacy.formulary_medicine</c> and its owned interactions and linked SKUs. The strength and the dose range are columns of
/// the medicine. Money-like decimals are <c>numeric(12,4)</c> (strengths and mg/kg). <c>formulary_medicine_sku.sku_id</c> is UNIQUE so the
/// one-SKU-one-medicine rule has a database guard (<c>ix_formulary_medicine_sku</c>). Every id, owned ones included, is <c>ValueGeneratedNever()</c>
/// (ADR-021).
/// </summary>
public sealed class FormularyMedicineConfiguration : IEntityTypeConfiguration<FormularyMedicineAggregate>
{
    public void Configure(EntityTypeBuilder<FormularyMedicineAggregate> builder)
    {
        builder.ToTable("formulary_medicine", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(FormularyRules.MaxCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(FormularyRules.MaxNameLength).IsRequired();
        builder.Property(x => x.ActiveIngredients).HasColumnName("active_ingredients").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.DrugClasses).HasColumnName("drug_classes").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.Form).HasColumnName("form").HasEnumerationValue().IsRequired();
        builder.Property(x => x.DefaultRoute).HasColumnName("default_route").HasEnumerationValue().IsRequired();
        builder.Property(x => x.AllowedRouteValues).HasColumnName("allowed_routes").HasColumnType("integer[]").IsRequired();
        builder.Property(x => x.DosingBasis).HasColumnName("dosing_basis").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Contraindications).HasColumnName("contraindications").HasMaxLength(FormularyRules.MaxContraindicationsLength).IsRequired();
        builder.Property(x => x.IsControlled).HasColumnName("is_controlled").IsRequired();
        builder.Property(x => x.ControlledSchedule).HasColumnName("controlled_schedule").HasMaxLength(ControlledDrugRules.MaxScheduleLength);
        builder.Property(x => x.ControlledChangeReason).HasColumnName("controlled_change_reason").HasMaxLength(FormularyRules.MaxReasonLength);
        builder.Property(x => x.DispensingUnit).HasColumnName("dispensing_unit").HasEnumerationValue().IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.OwnsOne(x => x.Strength, strength =>
        {
            strength.Property(p => p.Amount).HasColumnName("strength_amount").HasColumnType("numeric(12,4)").IsRequired();
            strength.Property(p => p.Unit).HasColumnName("strength_unit").HasEnumerationValue().IsRequired();
            strength.Property(p => p.PerQuantity).HasColumnName("strength_per_quantity").HasColumnType("numeric(12,4)").IsRequired();
            strength.Property(p => p.PerUnit).HasColumnName("strength_per_unit").HasEnumerationValue().IsRequired();
            strength.Ignore(p => p.AmountInMilligrams);
        });
        builder.Navigation(x => x.Strength).IsRequired();

        builder.OwnsOne(x => x.DoseRange, range =>
        {
            range.Property(p => p.MinMgPerKg).HasColumnName("dose_min_mg_per_kg").HasColumnType("numeric(12,4)");
            range.Property(p => p.MaxMgPerKg).HasColumnName("dose_max_mg_per_kg").HasColumnType("numeric(12,4)");
        });

        builder.OwnsMany(x => x.Interactions, interaction =>
        {
            interaction.ToTable("formulary_interaction", "pharmacy");
            interaction.WithOwner().HasForeignKey("formulary_medicine_id");
            interaction.HasKey("Id");
            interaction.Property(p => p.Id).ValueGeneratedNever();
            interaction.Property(p => p.InteractsWithMedicineId).HasColumnName("interacts_with_medicine_id");
            interaction.Property(p => p.InteractsWithTerm).HasColumnName("interacts_with_term").HasMaxLength(FormularyRules.MaxTermLength);
            interaction.Property(p => p.Severity).HasColumnName("severity").HasEnumerationValue().IsRequired();
            interaction.Property(p => p.Note).HasColumnName("note").HasMaxLength(FormularyRules.MaxInteractionNoteLength).IsRequired();
            interaction.HasIndex("formulary_medicine_id").HasDatabaseName("ix_formulary_interaction_medicine");
        });
        builder.Navigation(x => x.Interactions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.LinkedSkus, link =>
        {
            link.ToTable("formulary_medicine_sku", "pharmacy");
            link.WithOwner().HasForeignKey("formulary_medicine_id");
            link.HasKey("Id");
            link.Property(p => p.Id).ValueGeneratedNever();
            link.Property(p => p.SkuId).HasColumnName("sku_id").IsRequired();
            link.HasIndex(p => p.SkuId).IsUnique().HasDatabaseName("ix_formulary_medicine_sku");
            link.HasIndex("formulary_medicine_id").HasDatabaseName("ix_formulary_medicine_sku_medicine");
        });
        builder.Navigation(x => x.LinkedSkus).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.AllowedRoutes);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_formulary_medicine_code");
        builder.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("ix_formulary_medicine_active_name");
    }
}
