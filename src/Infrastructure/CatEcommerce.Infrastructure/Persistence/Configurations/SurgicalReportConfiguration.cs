using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-SRG-001 §8.2 / §12.1 (ADR-165) -- <c>clinic.surgical_report</c>: one row per surgery (the primary key IS the surgery id) and its owned, append-only addenda
/// (<c>surgical_report_addendum</c>). Once <c>finalised_on</c> is set the row can never be updated and no report row is ever deleted (<c>trg_immutable_surgical_report</c>, hand-written in
/// the <c>AddSurgicalReport</c> migration); an addendum row can never be updated or deleted (<c>trg_immutable_surgical_report_addendum</c>; both renamed by the integrator migration <c>RunSurgicalReportGuardsBeforeRetentionGuard</c> so they fire before the retention guard). No foreign keys to the surgery, cat or visit (ADR-011).
/// </summary>
public sealed class SurgicalReportConfiguration : IEntityTypeConfiguration<SurgicalReportAggregate>
{
    public void Configure(EntityTypeBuilder<SurgicalReportAggregate> builder)
    {
        builder.ToTable("surgical_report", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.SurgeryId).HasColumnName("surgery_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.ProcedurePerformed).HasColumnName("procedure_performed").HasMaxLength(SurgeryRules.ReportTextMax).IsRequired();
        builder.Property(x => x.Findings).HasColumnName("findings").HasMaxLength(SurgeryRules.ReportTextMax).IsRequired();
        builder.Property(x => x.Technique).HasColumnName("technique").HasMaxLength(SurgeryRules.ReportTextMax).IsRequired();
        builder.Property(x => x.SutureMaterial).HasColumnName("suture_material").HasMaxLength(500).IsRequired();
        builder.Property(x => x.HadComplications).HasColumnName("had_complications").IsRequired();
        builder.Property(x => x.Complications).HasColumnName("complications").HasMaxLength(SurgeryRules.ReportTextMax);
        builder.Property(x => x.SurgeonId).HasColumnName("surgeon_id").IsRequired();
        builder.Property(x => x.AssistantId).HasColumnName("assistant_id").IsRequired();
        builder.Property(x => x.FinalisedOn).HasColumnName("finalised_on").HasColumnType("timestamptz");
        builder.Property(x => x.FinalisedBy).HasColumnName("finalised_by");
        builder.Ignore(x => x.IsFinalised);

        builder.OwnsMany(x => x.Addenda, a =>
        {
            a.ToTable("surgical_report_addendum", "clinic");
            a.WithOwner().HasForeignKey("report_id");
            a.HasKey("Id");
            a.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            a.Property(p => p.Text).HasColumnName("text").HasMaxLength(SurgeryRules.AddendumMax).IsRequired();
            a.Property(p => p.AddedBy).HasColumnName("added_by").IsRequired();
            a.Property(p => p.AddedOn).HasColumnName("added_on").HasColumnType("timestamptz").IsRequired();
            a.HasIndex("report_id").HasDatabaseName("ix_surgical_report_addendum_report");
        });
        builder.Metadata.FindNavigation(nameof(SurgicalReportAggregate.Addenda))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.CatId).HasDatabaseName("ix_surgical_report_cat");
    }
}
