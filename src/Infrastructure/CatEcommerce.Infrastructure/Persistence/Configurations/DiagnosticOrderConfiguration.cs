using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-EMR-001 §8.5 / §12.1 / ADR-124 -- <c>clinic.diagnostic_order</c> and its owned collections: requested analytes,
/// result sets (with their values, <b>nested</b> -- V2) and attachments. Result sets, values and attachments are
/// append-only by trigger (ADR-118): a correction is a new set naming the one it supersedes. The order row itself
/// changes (<c>Pending</c> to <c>Resulted</c> or <c>Cancelled</c>) but is never deleted. A diagnostic stores
/// <b>no customer id</b>, and an attached file is referenced by id, once (unique index on <c>file_id</c>).
/// </summary>
public sealed class DiagnosticOrderConfiguration : IEntityTypeConfiguration<DiagnosticOrderAggregate>
{
    public void Configure(EntityTypeBuilder<DiagnosticOrderAggregate> builder)
    {
        builder.ToTable("diagnostic_order", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(DiagnosticRules.MaxDescriptionLength);
        builder.Property(x => x.OrderedBy).HasColumnName("ordered_by").IsRequired();
        builder.Property(x => x.OrderedOn).HasColumnName("ordered_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.CancelledReason).HasColumnName("cancelled_reason").HasMaxLength(DiagnosticRules.MaxCancelReasonLength);
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        // HO-BIL-001 Phase J: the price of the test when it was ordered (null before Phase J) and the tax class it bills under.
        builder.Property(x => x.UnitPriceAmount).HasColumnName("unit_price_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.UnitPriceCurrency).HasColumnName("unit_price_currency_code").HasColumnType("char(3)");
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id");
        builder.Ignore(x => x.UnitPrice);

        builder.OwnsMany(x => x.RequestedAnalytes, analyte =>
        {
            analyte.ToTable("diagnostic_requested_analyte", "clinic");
            analyte.WithOwner().HasForeignKey("diagnostic_order_id");
            analyte.HasKey("Id");
            analyte.Property(p => p.Id).ValueGeneratedNever();
            analyte.Property(p => p.AnalyteId).HasColumnName("analyte_id").IsRequired();
            analyte.Property(p => p.Code).HasColumnName("code").HasMaxLength(DiagnosticRules.MaxAnalyteCodeLength).IsRequired();
            analyte.Property(p => p.Name).HasColumnName("name").HasMaxLength(ClinicalNoteRules.MaxCatalogueNameLength).IsRequired();
            analyte.HasIndex("diagnostic_order_id").HasDatabaseName("ix_diagnostic_requested_analyte_order_id");
        });
        builder.Navigation(x => x.RequestedAnalytes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.ResultSets, set =>
        {
            set.ToTable("diagnostic_result_set", "clinic");
            set.WithOwner().HasForeignKey("diagnostic_order_id");
            set.HasKey("Id");
            set.Property(p => p.Id).ValueGeneratedNever();
            set.Property(p => p.ReportedOn).HasColumnName("reported_on").HasColumnType("date").IsRequired();
            set.Property(p => p.Note).HasColumnName("note").HasMaxLength(DiagnosticRules.MaxResultNoteLength);
            set.Property(p => p.SupersedesResultSetId).HasColumnName("supersedes_result_set_id");
            set.Property(p => p.RecordedBy).HasColumnName("recorded_by").IsRequired();
            set.Property(p => p.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
            set.HasIndex("diagnostic_order_id").HasDatabaseName("ix_diagnostic_result_set_order_id");

            set.OwnsMany(p => p.Values, value =>
            {
                value.ToTable("diagnostic_result_value", "clinic");
                value.WithOwner().HasForeignKey("result_set_id");
                value.HasKey("Id");
                value.Property(v => v.Id).ValueGeneratedNever();
                value.Property(v => v.AnalyteId).HasColumnName("analyte_id");
                value.Property(v => v.Name).HasColumnName("name").HasMaxLength(DiagnosticRules.MaxValueNameLength).IsRequired();
                value.Property(v => v.ValueKind).HasColumnName("value_kind").HasEnumerationValue().IsRequired();
                value.Property(v => v.NumericValue).HasColumnName("numeric_value").HasColumnType("numeric(14,4)");
                value.Property(v => v.TextValue).HasColumnName("text_value").HasMaxLength(DiagnosticRules.MaxTextValueLength);
                value.Property(v => v.Unit).HasColumnName("unit").HasMaxLength(DiagnosticRules.MaxUnitLength);
                value.Property(v => v.ReferenceLow).HasColumnName("reference_low").HasColumnType("numeric(14,4)");
                value.Property(v => v.ReferenceHigh).HasColumnName("reference_high").HasColumnType("numeric(14,4)");
                value.Property(v => v.NormalText).HasColumnName("normal_text").HasMaxLength(DiagnosticRules.MaxNormalTextLength);
                value.Property(v => v.Flag).HasColumnName("flag").HasEnumerationValue().IsRequired();
                value.Property(v => v.Position).HasColumnName("position").IsRequired();
                value.HasIndex("result_set_id").HasDatabaseName("ix_diagnostic_result_value_result_set_id");
            });
            set.Navigation(p => p.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(x => x.ResultSets).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Attachments, attachment =>
        {
            attachment.ToTable("diagnostic_attachment", "clinic");
            attachment.WithOwner().HasForeignKey("diagnostic_order_id");
            attachment.HasKey("Id");
            attachment.Property(p => p.Id).ValueGeneratedNever();
            attachment.Property(p => p.FileId).HasColumnName("file_id").IsRequired();
            attachment.Property(p => p.ResultDate).HasColumnName("result_date").HasColumnType("date").IsRequired();
            attachment.Property(p => p.Note).HasColumnName("note").HasMaxLength(DiagnosticRules.MaxAttachmentNoteLength);
            attachment.Property(p => p.AttachedBy).HasColumnName("attached_by").IsRequired();
            attachment.Property(p => p.AttachedOn).HasColumnName("attached_on").HasColumnType("timestamptz").IsRequired();
            attachment.HasIndex("diagnostic_order_id").HasDatabaseName("ix_diagnostic_attachment_order_id");
            // A file is attached once (§12.1, FileAlreadyAttached).
            attachment.HasIndex(p => p.FileId).IsUnique().HasDatabaseName("ix_diagnostic_attachment_file_id");
        });
        builder.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.CatId, x.OrderedOn }).IsDescending(false, true).HasDatabaseName("ix_diagnostic_order_cat_ordered_on");
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_diagnostic_order_branch_status");
        builder.HasIndex(x => x.VisitId).HasDatabaseName("ix_diagnostic_order_visit_id");
    }
}
