using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.1 / 12.1 -- <c>billing.bank_match</c>. The two filtered unique indexes (active line, active payment) are what make
/// a match 1:1 under concurrency; an unmatch keeps the row.</summary>
public sealed class BankMatchConfiguration : IEntityTypeConfiguration<BankMatchAggregate>
{
    public void Configure(EntityTypeBuilder<BankMatchAggregate> builder)
    {
        builder.ToTable("bank_match", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StatementId).HasColumnName("statement_id").IsRequired();
        builder.Property(x => x.StatementLineId).HasColumnName("statement_line_id").IsRequired();
        builder.Property(x => x.PaymentId).HasColumnName("payment_id").IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.MatchedBy).HasColumnName("matched_by").IsRequired();
        builder.Property(x => x.MatchedOn).HasColumnName("matched_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UnmatchedOn).HasColumnName("unmatched_on").HasColumnType("timestamptz");
        builder.Property(x => x.UnmatchedBy).HasColumnName("unmatched_by");
        builder.Property(x => x.UnmatchReason).HasColumnName("unmatch_reason").HasMaxLength(1000);
        builder.Ignore(x => x.IsActive);

        builder.HasIndex(x => x.StatementLineId).IsUnique().HasDatabaseName("ix_bank_match_line").HasFilter("unmatched_on IS NULL");
        builder.HasIndex(x => x.PaymentId).IsUnique().HasDatabaseName("ix_bank_match_payment").HasFilter("unmatched_on IS NULL");
        builder.HasIndex(x => x.StatementId).HasDatabaseName("ix_bank_match_statement");

        builder.ConfigureRootTail();
    }
}
