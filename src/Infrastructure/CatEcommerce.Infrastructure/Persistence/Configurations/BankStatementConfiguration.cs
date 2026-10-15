using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.1 / 12.1 -- <c>billing.bank_statement</c> and its lines. The file itself is never stored (parsed in memory);
/// the unique (account, sha256) index refuses a second import of the same file; descriptions are stored card-masked (C-5).</summary>
public sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatementAggregate>
{
    public void Configure(EntityTypeBuilder<BankStatementAggregate> builder)
    {
        builder.ToTable("bank_statement", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.BankAccountLabel).HasColumnName("bank_account_label").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PeriodFrom).HasColumnName("period_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.PeriodTo).HasColumnName("period_to").HasColumnType("date").IsRequired();
        builder.Property(x => x.SourceFileName).HasColumnName("source_file_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.SourceSha256).HasColumnName("source_sha256").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.ImportedBy).HasColumnName("imported_by").IsRequired();
        builder.Property(x => x.ImportedOn).HasColumnName("imported_on").HasColumnType("timestamptz").IsRequired();

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("bank_statement_line", "billing");
            line.WithOwner().HasForeignKey("statement_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.LineNumber).HasColumnName("line_number").IsRequired();
            line.Property(l => l.TransactionDate).HasColumnName("transaction_date").HasColumnType("date").IsRequired();
            line.Property(l => l.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.Direction).HasColumnName("direction").HasEnumerationConversion().IsRequired();
            line.Property(l => l.Reference).HasColumnName("reference").HasMaxLength(100);
            line.Property(l => l.Description).HasColumnName("description").HasMaxLength(500);
            line.Property(l => l.BankTransactionId).HasColumnName("bank_transaction_id").HasMaxLength(100);
            line.HasIndex(l => l.TransactionDate).HasDatabaseName("ix_bank_statement_line_date");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.BankAccountLabel, x.SourceSha256 }).IsUnique().HasDatabaseName("ix_bank_statement_source");
        builder.HasIndex(x => new { x.BranchId, x.ImportedOn }).HasDatabaseName("ix_bank_statement_branch_imported_on");

        builder.ConfigureRootTail();
    }
}
