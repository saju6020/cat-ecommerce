using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-BIL-001 §8.9 / §12.1 -- <c>billing.credit_note</c> and <c>credit_note_line</c>: append-only (the database refuses any change,
/// ADR-180). Its own gapless number series (ADR-179); the unique number and sequence indexes are the backstop.
/// </summary>
public sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNoteAggregate>
{
    public void Configure(EntityTypeBuilder<CreditNoteAggregate> builder)
    {
        builder.ToTable("credit_note", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(40).IsRequired();
        builder.Property(x => x.FinancialYear).HasColumnName("financial_year").HasMaxLength(9).IsRequired();
        builder.Property(x => x.SequenceNumber).HasColumnName("sequence_number").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.NetTotal).HasColumnName("net_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.TaxTotal).HasColumnName("tax_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.ReleasedToCustomerCredit).HasColumnName("released_to_customer_credit").HasMoneyColumn().IsRequired();
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.IssuedBy).HasColumnName("issued_by").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("credit_note_line", "billing");
            line.WithOwner().HasForeignKey("credit_note_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.InvoiceLineId).HasColumnName("invoice_line_id").IsRequired();
            line.Property(l => l.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
            line.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,3)");
            line.Property(l => l.RequestedAmount).HasColumnName("requested_amount").HasMoneyColumn();
            line.Property(l => l.NetAmount).HasColumnName("net_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.TaxRatePercent).HasColumnName("tax_rate_percent").HasColumnType("numeric(5,2)").IsRequired();
            line.Property(l => l.TaxAmount).HasColumnName("tax_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.GrossAmount).HasColumnName("gross_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.SortOrder).HasColumnName("sort_order").IsRequired();
            line.HasIndex(l => l.InvoiceLineId).HasDatabaseName("ix_credit_note_line_invoice_line");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_credit_note_number");
        builder.HasIndex(x => new { x.BranchId, x.FinancialYear, x.SequenceNumber }).IsUnique().HasDatabaseName("ix_credit_note_sequence");
        builder.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_credit_note_invoice");
        builder.HasIndex(x => new { x.CustomerId, x.IssuedOn }).HasDatabaseName("ix_credit_note_customer_issued_on");
        builder.HasIndex(x => new { x.IssuedBy, x.IdempotencyKey }).IsUnique().HasDatabaseName("ix_credit_note_idempotency")
            .HasFilter("idempotency_key IS NOT NULL");

        builder.ConfigureRootTail();
    }
}
