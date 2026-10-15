using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.11 / 12.1 -- <c>billing.loyalty_account</c>: the cached balance of a customer ledger; the id is the customer id.</summary>
public sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccountAggregate>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccountAggregate> builder)
    {
        builder.ToTable("loyalty_account", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Balance).HasColumnName("balance").IsRequired();
        builder.Ignore(x => x.CustomerId);

        builder.ConfigureRootTail();
    }
}

/// <summary>HO-BIL-001 section 8.11 / 12.1 -- <c>billing.loyalty_entry</c>: the append-only points ledger. The filtered unique indexes are the idempotency
/// of the handlers (accrual per invoice, reversal per credit note, merge transfer per merged customer); a redemption is deliberately outside
/// them -- it may be applied, removed and applied again on one draft.</summary>
public sealed class LoyaltyEntryConfiguration : IEntityTypeConfiguration<LoyaltyEntryAggregate>
{
    public void Configure(EntityTypeBuilder<LoyaltyEntryAggregate> builder)
    {
        builder.ToTable("loyalty_entry", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.Points).HasColumnName("points").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        builder.Property(x => x.CreditNoteId).HasColumnName("credit_note_id");
        builder.Property(x => x.SourceCustomerId).HasColumnName("source_customer_id");
        builder.Property(x => x.Shortfall).HasColumnName("shortfall").IsRequired();
        builder.Property(x => x.IdempotentSource).HasColumnName("idempotent_source").IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.Kind, x.InvoiceId }).IsUnique().HasDatabaseName("ix_loyalty_entry_kind_invoice")
            .HasFilter("idempotent_source AND invoice_id IS NOT NULL");
        builder.HasIndex(x => new { x.Kind, x.CreditNoteId }).IsUnique().HasDatabaseName("ix_loyalty_entry_kind_credit_note")
            .HasFilter("idempotent_source AND credit_note_id IS NOT NULL");
        builder.HasIndex(x => new { x.Kind, x.SourceCustomerId }).IsUnique().HasDatabaseName("ix_loyalty_entry_kind_source_customer")
            .HasFilter("idempotent_source AND source_customer_id IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.OccurredOn }).HasDatabaseName("ix_loyalty_entry_customer_occurred_on");

        builder.ConfigureRootTail();
    }
}
