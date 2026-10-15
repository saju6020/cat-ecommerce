using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 §8.9 / §12.1 -- <c>billing.refund</c>: money paid back; append-only (ADR-180). No card column (C-5).</summary>
public sealed class RefundConfiguration : IEntityTypeConfiguration<RefundAggregate>
{
    public void Configure(EntityTypeBuilder<RefundAggregate> builder)
    {
        builder.ToTable("refund", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PaymentId).HasColumnName("payment_id").IsRequired();
        builder.Property(x => x.CreditNoteId).HasColumnName("credit_note_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.IssuedBy).HasColumnName("issued_by").IsRequired();
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.BusinessDate).HasColumnName("business_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);

        builder.HasIndex(x => new { x.IssuedBy, x.IdempotencyKey }).IsUnique().HasDatabaseName("ix_refund_idempotency")
            .HasFilter("idempotency_key IS NOT NULL");
        builder.HasIndex(x => x.PaymentId).HasDatabaseName("ix_refund_payment");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_refund_customer");
        builder.HasIndex(x => new { x.BranchId, x.BusinessDate }).HasDatabaseName("ix_refund_branch_business_date");

        builder.ConfigureRootTail();
    }
}
