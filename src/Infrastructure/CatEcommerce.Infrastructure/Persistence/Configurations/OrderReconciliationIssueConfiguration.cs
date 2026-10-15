using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ORD-001 §12.1 (Phase E) -- <c>ordering.reconciliation_issue</c> (ADR-205): what a staff member entered when the recording failed. The
/// error column holds an exception type and a fixed message, never an exception message. <c>(status, raised_on)</c> serves the accountants' list.</summary>
public sealed class OrderReconciliationIssueConfiguration : IEntityTypeConfiguration<OrderReconciliationIssueAggregate>
{
    public void Configure(EntityTypeBuilder<OrderReconciliationIssueAggregate> builder)
    {
        builder.ConfigureOrderingRoot("reconciliation_issue");

        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Amount).HasMoney("amount").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.PaymentReference).HasColumnName("payment_reference").HasMaxLength(OrderRules.MaxTransferReferenceLength);
        builder.Property(x => x.PaymentReceivedOn).HasInstant("payment_received_on");
        builder.Property(x => x.ErrorSummary).HasColumnName("error_summary").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(x => x.RaisedBy).HasColumnName("raised_by").IsRequired();
        builder.Property(x => x.RaisedOn).HasInstant("raised_on").IsRequired();
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(x => x.ResolvedOn).HasInstant("resolved_on");
        builder.Property(x => x.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(OrderRules.MaxReasonLength);

        builder.HasIndex(x => new { x.Status, x.RaisedOn }).HasDatabaseName("ix_reconciliation_issue_status_raised");
        builder.HasIndex(x => x.OrderId).HasDatabaseName("ix_reconciliation_issue_order");
        builder.HasIndex(x => new { x.BranchId, x.Status, x.RaisedOn }).HasDatabaseName("ix_reconciliation_issue_branch_status_raised");
    }
}
