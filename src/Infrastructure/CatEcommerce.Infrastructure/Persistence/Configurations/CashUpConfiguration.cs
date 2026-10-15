using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.10 / 12.1 -- <c>billing.cash_up</c>: one till count per (branch, business date), its expected totals
/// frozen on close. No delete (D-6); the unique pair is also what makes a second "create" a violation, not a second row.</summary>
public sealed class CashUpConfiguration : IEntityTypeConfiguration<CashUpAggregate>
{
    public void Configure(EntityTypeBuilder<CashUpAggregate> builder)
    {
        builder.ToTable("cash_up", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.BusinessDate).HasColumnName("business_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.CountedCash).HasColumnName("counted_cash").HasMoneyColumn();
        builder.Property(x => x.CountedBy).HasColumnName("counted_by");
        builder.Property(x => x.CountedOn).HasColumnName("counted_on").HasColumnType("timestamptz");
        builder.Property(x => x.Variance).HasColumnName("variance").HasMoneyColumn();
        builder.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(1000);
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
        builder.Property(x => x.ClosedOn).HasColumnName("closed_on").HasColumnType("timestamptz");

        builder.OwnsOne(x => x.ExpectedSnapshot, snapshot =>
        {
            snapshot.Property(s => s.CashTotal).HasColumnName("expected_cash_total").HasMoneyColumn();
            snapshot.Property(s => s.CashCount).HasColumnName("expected_cash_count");
            snapshot.Property(s => s.CardTotal).HasColumnName("expected_card_total").HasMoneyColumn();
            snapshot.Property(s => s.CardCount).HasColumnName("expected_card_count");
            snapshot.Property(s => s.TransferTotal).HasColumnName("expected_transfer_total").HasMoneyColumn();
            snapshot.Property(s => s.TransferCount).HasColumnName("expected_transfer_count");
            snapshot.Property(s => s.CashRefunds).HasColumnName("expected_cash_refunds").HasMoneyColumn();
            snapshot.Property(s => s.CashReversals).HasColumnName("expected_cash_reversals").HasMoneyColumn();
            snapshot.Property(s => s.CarriedIn).HasColumnName("expected_carried_in").HasMoneyColumn();
            snapshot.Property(s => s.ExpectedCash).HasColumnName("expected_cash").HasMoneyColumn();
        });

        builder.HasIndex(x => new { x.BranchId, x.BusinessDate }).IsUnique().HasDatabaseName("ix_cash_up_branch_date");

        builder.ConfigureRootTail();
    }
}
