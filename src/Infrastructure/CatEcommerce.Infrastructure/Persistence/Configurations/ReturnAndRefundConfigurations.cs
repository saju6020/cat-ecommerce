using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ORD-001 §12.1 (Phase G) -- <c>ordering.return_request</c> and its owned <c>return_line</c>. A return is the customer's claim and staff's decision: the
/// status moves, the lines record what came back (<c>receipts</c>, jsonb) and what was decided. Nothing is deleted (D-9).</summary>
public sealed class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequestAggregate>
{
    public void Configure(EntityTypeBuilder<ReturnRequestAggregate> builder)
    {
        builder.ConfigureOrderingRoot("return_request");

        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.RequestedOn).HasInstant("requested_on").IsRequired();
        builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
        builder.Property(x => x.DecidedOn).HasInstant("decided_on");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(OrderRules.MaxReasonLength);
        builder.Property(x => x.ReceivedBy).HasColumnName("received_by");
        builder.Property(x => x.ReceivedOn).HasInstant("received_on");
        builder.Property(x => x.ClosedOn).HasInstant("closed_on");

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("return_line", OrderingMapping.Schema);
            line.WithOwner().HasForeignKey("return_request_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.OrderLineId).HasColumnName("order_line_id").IsRequired();
            line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
            line.Property(l => l.ReasonCode).HasEnumeration("reason_code");
            line.Property(l => l.Condition).HasEnumeration("condition");
            line.Property(l => l.CustomerNote).HasColumnName("customer_note").HasMaxLength(OrderRules.MaxReasonLength);
            line.Property(l => l.RequiresDefectConfirmation).HasColumnName("requires_defect_confirmation").IsRequired();
            line.Property(l => l.DefectConfirmed).HasColumnName("defect_confirmed").IsRequired();
            line.Property(l => l.DefectNote).HasColumnName("defect_note").HasMaxLength(OrderRules.MaxReasonLength);
            line.Property(l => l.QuantityReceived).HasColumnName("quantity_received").IsRequired();
            line.Property(l => l.Receipts).HasJsonbList("receipts");
            line.HasIndex("return_request_id").HasDatabaseName("ix_return_line_return");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);


        builder.HasIndex(x => x.OrderId).HasDatabaseName("ix_return_request_order");
        builder.HasIndex(x => new { x.Status, x.RequestedOn }).HasDatabaseName("ix_return_request_status_requested");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_return_request_customer");
    }
}

/// <summary>HO-ORD-001 §12.1 (Phase G, ADR-211) -- <c>ordering.order_refund</c>, append-only (the migration's triggers refuse update, delete and truncate).</summary>
public sealed class OrderRefundConfiguration : IEntityTypeConfiguration<OrderRefundAggregate>
{
    public void Configure(EntityTypeBuilder<OrderRefundAggregate> builder)
    {
        builder.ConfigureOrderingRoot("order_refund");

        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.ReturnRequestId).HasColumnName("return_request_id");
        builder.Property(x => x.Method).HasEnumeration("method");
        builder.Property(x => x.Amount).HasMoney("amount").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(OrderRules.MaxTransferReferenceLength);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(OrderRules.MaxReasonLength).IsRequired();
        builder.Property(x => x.IssuedBy).HasColumnName("issued_by").IsRequired();
        builder.Property(x => x.IssuedOn).HasInstant("issued_on").IsRequired();
        builder.Property(x => x.CreditNoteId).HasColumnName("credit_note_id");

        builder.HasIndex(x => x.OrderId).HasDatabaseName("ix_order_refund_order");
    }
}
