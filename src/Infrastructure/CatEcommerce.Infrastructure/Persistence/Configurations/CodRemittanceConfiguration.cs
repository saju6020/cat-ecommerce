using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 section 8.1 / 12.1 -- <c>billing.cod_remittance</c> and its lines: append-only (ADR-180, D-6).</summary>
public sealed class CodRemittanceConfiguration : IEntityTypeConfiguration<CodRemittanceAggregate>
{
    public void Configure(EntityTypeBuilder<CodRemittanceAggregate> builder)
    {
        builder.ToTable("cod_remittance", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CourierCode).HasColumnName("courier_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by").IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("cod_remittance_line", "billing");
            line.WithOwner().HasForeignKey("remittance_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.PaymentId).HasColumnName("payment_id").IsRequired();
            line.Property(l => l.AmountRemitted).HasColumnName("amount_remitted").HasMoneyColumn().IsRequired();
            line.HasIndex(l => l.PaymentId).HasDatabaseName("ix_cod_remittance_line_payment");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.Unapplied);

        builder.HasIndex(x => new { x.BranchId, x.CourierCode, x.ReceivedOn }).HasDatabaseName("ix_cod_remittance_branch_courier");

        builder.ConfigureRootTail();
    }
}
