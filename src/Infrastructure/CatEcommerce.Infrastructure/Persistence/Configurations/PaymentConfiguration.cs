using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-BIL-001 §8.8 / §12.1 -- <c>billing.payment</c>: <b>a record of money received</b>, method-independent (ADR-182). There is no
/// token column, no provider column and no card-number column (C-5). The core columns are immutable in the database
/// (<c>trg_payment_immutable</c>); where the money went is the append-only <c>payment_allocation_entry</c>; the receipt number comes
/// from <c>billing.receipt_number_seq</c> (gaps allowed -- a receipt number is not a legal document number).
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentAggregate>
{
    public void Configure(EntityTypeBuilder<PaymentAggregate> builder)
    {
        builder.ToTable("payment", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(100);
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by").IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        builder.Property(x => x.ApprovedOn).HasColumnName("approved_on").HasColumnType("timestamptz");
        builder.Property(x => x.RejectedBy).HasColumnName("rejected_by");
        builder.Property(x => x.RejectedOn).HasColumnName("rejected_on").HasColumnType("timestamptz");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
        builder.Property(x => x.AcceptedOn).HasColumnName("accepted_on").HasColumnType("timestamptz");
        builder.Property(x => x.BusinessDate).HasColumnName("business_date").HasColumnType("date");
        builder.Property(x => x.ReceiptNumber).HasColumnName("receipt_number").HasColumnType("bigint")
            .HasDefaultValueSql("nextval('billing.receipt_number_seq')").ValueGeneratedOnAdd();
        builder.Property(x => x.ReversedBy).HasColumnName("reversed_by");
        builder.Property(x => x.ReversedOn).HasColumnName("reversed_on").HasColumnType("timestamptz");
        builder.Property(x => x.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(1000);
        builder.Property(x => x.ReversalBusinessDate).HasColumnName("reversal_business_date").HasColumnType("date");
        builder.Property(x => x.RefundedTotal).HasColumnName("refunded_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.ApprovalWaiverReason).HasColumnName("approval_waiver_reason").HasMaxLength(30);
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);

        builder.OwnsOne(x => x.Collector, collector =>
        {
            collector.Property(c => c.Kind).HasColumnName("collector_kind").HasEnumerationConversion().IsRequired();
            collector.Property(c => c.StaffAccountId).HasColumnName("collector_staff_account_id");
            collector.Property(c => c.CourierCode).HasColumnName("collector_courier_code").HasMaxLength(30);
            collector.Property(c => c.CourierName).HasColumnName("collector_courier_name").HasMaxLength(100);
        });

        builder.OwnsOne(x => x.Earmark, earmark =>
        {
            earmark.Property(e => e.Kind).HasColumnName("earmark_kind").HasEnumerationConversion().IsRequired();
            earmark.Property(e => e.SourceId).HasColumnName("earmark_source_id").IsRequired();
            earmark.Property(e => e.Label).HasColumnName("earmark_label").HasMaxLength(200).IsRequired();
            earmark.Property(e => e.ReleasedOn).HasColumnName("earmark_released_on").HasColumnType("timestamptz");
            earmark.Property(e => e.ReleasedBy).HasColumnName("earmark_released_by");
            earmark.Property(e => e.ReleaseReason).HasColumnName("earmark_release_reason").HasMaxLength(1000);
            earmark.HasIndex(e => new { e.Kind, e.SourceId }).HasDatabaseName("ix_payment_earmark");
        });

        builder.OwnsMany(x => x.Entries, entry =>
        {
            entry.ToTable("payment_allocation_entry", "billing");
            entry.WithOwner().HasForeignKey("payment_id");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entry.Property(e => e.Kind).HasColumnName("kind").HasEnumerationConversion().IsRequired();
            entry.Property(e => e.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entry.Property(e => e.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
            entry.Property(e => e.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamptz").IsRequired();
            entry.Property(e => e.By).HasColumnName("performed_by").IsRequired();
            entry.Property(e => e.ReleaseReason).HasColumnName("release_reason").HasNullableEnumerationConversion();
            entry.HasIndex(e => e.InvoiceId).HasDatabaseName("ix_payment_allocation_entry_invoice");
        });
        builder.Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.RequestedAllocations, requested =>
        {
            requested.ToTable("payment_requested_allocation", "billing");
            requested.WithOwner().HasForeignKey("payment_id");
            requested.HasKey(r => r.Id);
            requested.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            requested.Property(r => r.InvoiceId).HasColumnName("invoice_id").IsRequired();
            requested.Property(r => r.Amount).HasColumnName("amount").HasMoneyColumn().IsRequired();
        });
        builder.Navigation(x => x.RequestedAllocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.NetAllocated);
        builder.Ignore(x => x.Unallocated);
        builder.Ignore(x => x.IsFreeCredit);

        builder.HasIndex(x => new { x.RecordedBy, x.IdempotencyKey }).IsUnique().HasDatabaseName("ix_payment_idempotency")
            .HasFilter("idempotency_key IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("ix_payment_customer_status");
        builder.HasIndex(x => new { x.BranchId, x.BusinessDate }).HasDatabaseName("ix_payment_branch_business_date");
        builder.HasIndex(x => new { x.Method, x.Status, x.ReceivedOn }).HasDatabaseName("ix_payment_method_status_received_on");
        builder.HasIndex(x => new { x.Status, x.RecordedOn }).HasDatabaseName("ix_payment_status_recorded_on");

        builder.ConfigureRootTail();
    }
}
