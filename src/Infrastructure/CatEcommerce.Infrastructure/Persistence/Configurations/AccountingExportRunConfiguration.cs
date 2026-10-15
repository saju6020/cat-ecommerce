using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.1 / 12.1 -- <c>billing.accounting_export_run</c>: who exported what and its fingerprint; append-only (ADR-190).</summary>
public sealed class AccountingExportRunConfiguration : IEntityTypeConfiguration<AccountingExportRunAggregate>
{
    public void Configure(EntityTypeBuilder<AccountingExportRunAggregate> builder)
    {
        builder.ToTable("accounting_export_run", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.From).HasColumnName("range_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.To).HasColumnName("range_to").HasColumnType("date").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.InvoiceCount).HasColumnName("invoice_count").IsRequired();
        builder.Property(x => x.InvoiceLineCount).HasColumnName("invoice_line_count").IsRequired();
        builder.Property(x => x.PaymentCount).HasColumnName("payment_count").IsRequired();
        builder.Property(x => x.AllocationCount).HasColumnName("allocation_count").IsRequired();
        builder.Property(x => x.CreditNoteCount).HasColumnName("credit_note_count").IsRequired();
        builder.Property(x => x.RefundCount).HasColumnName("refund_count").IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.RunBy).HasColumnName("run_by").IsRequired();
        builder.Property(x => x.RunOn).HasColumnName("run_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => x.RunOn).HasDatabaseName("ix_accounting_export_run_run_on");

        builder.ConfigureRootTail();
    }
}
