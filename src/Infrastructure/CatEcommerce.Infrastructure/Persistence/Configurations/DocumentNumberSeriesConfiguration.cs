using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 §8.7 / §12.1 (ADR-179) -- one row per <c>(branch, financial year, kind)</c>;
/// the unique index is the target of the <c>INSERT … ON CONFLICT DO NOTHING</c> in
/// <c>DocumentNumbering.EnsureSeriesAsync</c>, and <c>xmin</c> is what makes two concurrent takers collide.</summary>
public sealed class DocumentNumberSeriesConfiguration : IEntityTypeConfiguration<DocumentNumberSeriesAggregate>
{
    public void Configure(EntityTypeBuilder<DocumentNumberSeriesAggregate> builder)
    {
        builder.ToTable("document_number_series", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.FinancialYear).HasColumnName("financial_year").HasMaxLength(10).IsRequired();
        builder.Property(x => x.DocumentKind).HasColumnName("document_kind").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.NextSequence).HasColumnName("next_sequence").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.FinancialYear, x.DocumentKind }).IsUnique().HasDatabaseName("ix_document_number_series");

        builder.ConfigureRootTail();
    }
}
