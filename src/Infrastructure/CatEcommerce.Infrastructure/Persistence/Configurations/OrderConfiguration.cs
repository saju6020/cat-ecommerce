using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-ORD-001 §12.1 (Phase D) -- <c>ordering.sales_order</c> and its owned children: <c>order_line</c> (with <c>order_line_allocation</c>),
/// <c>order_status_change</c> and <c>order_payment_ref</c>; the fulfilment snapshot, the totals, the held payment, the shipment and the cash-on-delivery
/// collection are owned single objects stored on the order's own row. Money is <c>numeric(12,2)</c> in the order's one currency; list-valued fields
/// (<c>credit_note_ids</c>, a bundle line's components) are <c>jsonb</c>. Orders are financial records (D-9): nothing here is ever deleted -- the
/// database refuses it (<see cref="OrderingMigrationSql"/>), which is why the status history and the payment references are append-only.
///
/// Indexes: the number and the per-customer idempotency key are unique backstops (<c>ix_sales_order_customer_idempotency</c> -> 409
/// <c>DuplicateSubmission</c>); the others serve the staff queue, the expiry pass, the per-customer discount count, the webhook lookup and the
/// recall trace (<c>order_line_allocation (batch_id)</c>, D-4).
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderAggregate>
{
    public void Configure(EntityTypeBuilder<OrderAggregate> builder)
    {
        builder.ConfigureOrderingRoot("sales_order");

        builder.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(OrderRules.MaxOrderNumberLength).IsRequired();
        builder.Property(x => x.Channel).HasEnumeration("channel");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.FulfilmentMethod).HasEnumeration("fulfilment_method");
        builder.Property(x => x.PaymentMethod).HasEnumeration("payment_method");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.DiscountCodeId).HasColumnName("discount_code_id");
        builder.Property(x => x.DiscountCodeText).HasColumnName("discount_code_text").HasMaxLength(DiscountCodeRules.MaxCodeLength);
        builder.Property(x => x.DiscountReleased).HasColumnName("discount_released").IsRequired();
        builder.Property(x => x.QuoteFingerprint).HasColumnName("quote_fingerprint").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
        builder.Property(x => x.PlacedOn).HasInstant("placed_on").IsRequired();
        builder.Property(x => x.PaymentDueBy).HasInstant("payment_due_by");
        builder.Property(x => x.CustomerTransferReference).HasColumnName("customer_transfer_reference").HasMaxLength(OrderRules.MaxTransferReferenceLength);
        builder.Property(x => x.TransferReportedOn).HasInstant("transfer_reported_on");
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(40);
        builder.Property(x => x.CreditNoteIds).HasJsonbList("credit_note_ids");
        builder.Property(x => x.AmountPaid).HasMoney("amount_paid").IsRequired();
        builder.Property(x => x.AmountRefunded).HasMoney("amount_refunded").IsRequired();
        builder.Property(x => x.RefundDue).HasMoney("refund_due").IsRequired();
        builder.Property(x => x.ChangeGiven).HasMoney("change_given").IsRequired();
        builder.Property(x => x.ReadyForCollectionOn).HasInstant("ready_for_collection_on");
        builder.Property(x => x.CollectByDate).HasColumnName("collect_by_date").HasColumnType("date");
        builder.Property(x => x.DeliveredOn).HasInstant("delivered_on");
        builder.Property(x => x.DeliveryRecordedBy).HasColumnName("delivery_recorded_by");
        builder.Property(x => x.DeliveryFailureReason).HasNullableEnumeration("delivery_failure_reason");
        builder.Property(x => x.CancellationReason).HasNullableEnumeration("cancellation_reason");
        builder.Property(x => x.CancellationNote).HasColumnName("cancellation_note").HasMaxLength(OrderRules.MaxReasonLength);
        builder.Property(x => x.CancelledOn).HasInstant("cancelled_on");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancelledByCustomer).HasColumnName("cancelled_by_customer").IsRequired();
        builder.Property(x => x.ConfirmedOn).HasInstant("confirmed_on");
        builder.Property(x => x.PackingStartedOn).HasInstant("packing_started_on");
        builder.Property(x => x.CustomerAnonymised).HasColumnName("customer_anonymised").IsRequired();

        builder.OwnsOne(x => x.Fulfilment, f =>
        {
            f.Property(p => p.RecipientName).HasColumnName("recipient_name").HasMaxLength(OrderRules.MaxRecipientNameLength).IsRequired();
            f.Property(p => p.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(OrderRules.MaxRecipientPhoneLength);
            f.Property(p => p.Line1).HasColumnName("address_line1").HasMaxLength(DeliveryAddressRules.Line1MaxLength);
            f.Property(p => p.Line2).HasColumnName("address_line2").HasMaxLength(DeliveryAddressRules.Line2MaxLength);
            f.Property(p => p.City).HasColumnName("address_city").HasMaxLength(DeliveryAddressRules.CityMaxLength);
            f.Property(p => p.District).HasColumnName("address_district").HasMaxLength(DeliveryAddressRules.DistrictMaxLength);
            f.Property(p => p.PostalCode).HasColumnName("address_postal_code").HasMaxLength(DeliveryAddressRules.PostalCodeMaxLength);
            f.Property(p => p.DeliveryInstructions).HasColumnName("delivery_instructions").HasMaxLength(DeliveryAddressRules.DeliveryInstructionsMaxLength);
            f.Property(p => p.DeliveryZoneId).HasColumnName("delivery_zone_id");
            f.Property(p => p.ZoneName).HasColumnName("zone_name").HasMaxLength(DeliveryZoneRules.MaxNameLength);
            f.Property(p => p.ServiceLevel).HasNullableEnumeration("service_level");
            f.Property(p => p.SlotDate).HasColumnName("slot_date").HasColumnType("date");
            f.Property(p => p.SlotWindowLabel).HasColumnName("slot_window_label").HasMaxLength(30);
            f.Property(p => p.SlotWindowStart).HasColumnName("slot_window_start").HasColumnType("time");
            f.Property(p => p.SlotWindowEnd).HasColumnName("slot_window_end").HasColumnType("time");
            f.Property(p => p.PickupBranchId).HasColumnName("pickup_branch_id");
        });
        builder.Navigation(x => x.Fulfilment).IsRequired();

        builder.Property(x => x.DeliveryTaxAmount).HasMoney("delivery_tax_amount").IsRequired();

        builder.OwnsOne(x => x.Totals, t =>
        {
            t.Property(p => p.Subtotal).HasMoney("subtotal").IsRequired();
            t.Property(p => p.LineDiscountTotal).HasMoney("line_discount_total").IsRequired();
            t.Property(p => p.OrderDiscount).HasMoney("order_discount").IsRequired();
            t.Property(p => p.DeliveryCharge).HasMoney("delivery_charge").IsRequired();
            t.Property(p => p.TaxTotal).HasMoney("tax_total").IsRequired();
            t.Property(p => p.GrandTotal).HasMoney("grand_total").IsRequired();
        });
        builder.Navigation(x => x.Totals).IsRequired();

        builder.OwnsOne(x => x.HeldPayment, h =>
        {
            h.Property(p => p.Amount).HasMoney("held_amount").IsRequired();
            h.Property(p => p.Reference).HasColumnName("held_reference").HasMaxLength(OrderRules.MaxTransferReferenceLength).IsRequired();
            h.Property(p => p.ReceivedOn).HasInstant("held_received_on").IsRequired();
            h.Property(p => p.RecordedBy).HasColumnName("held_recorded_by").IsRequired();
            h.Property(p => p.RecordedOn).HasInstant("held_recorded_on").IsRequired();
            h.Property(p => p.PaymentId).HasColumnName("held_payment_id");
        });

        builder.OwnsOne(x => x.Shipment, s =>
        {
            s.Property(p => p.CourierId).HasColumnName("shipment_courier_id").IsRequired();
            s.Property(p => p.CourierName).HasColumnName("shipment_courier_name").HasMaxLength(OrderRules.MaxNameLength).IsRequired();
            s.Property(p => p.TrackingNumber).HasColumnName("shipment_tracking_number").HasMaxLength(OrderRules.MaxTrackingNumberLength);
            s.Property(p => p.TrackingUrl).HasColumnName("shipment_tracking_url").HasMaxLength(700);
            s.Property(p => p.DispatchedOn).HasInstant("shipment_dispatched_on").IsRequired();
            s.Property(p => p.DispatchedBy).HasColumnName("shipment_dispatched_by").IsRequired();
            s.Property(p => p.LastTrackingStatus).HasColumnName("shipment_last_status").HasMaxLength(100);
            s.Property(p => p.LastTrackingEventOn).HasInstant("shipment_last_event_on");
            s.HasIndex(p => p.TrackingNumber).HasDatabaseName("ix_sales_order_tracking").HasFilter("shipment_tracking_number IS NOT NULL");
        });

        builder.OwnsOne(x => x.CodCollection, c =>
        {
            c.Property(p => p.Amount).HasMoney("cod_amount").IsRequired();
            c.Property(p => p.CollectorKind).HasEnumeration("cod_collector_kind");
            c.Property(p => p.CollectorName).HasColumnName("cod_collector_name").HasMaxLength(OrderRules.MaxRecipientNameLength).IsRequired();
            c.Property(p => p.CollectorStaffId).HasColumnName("cod_collector_staff_id");
            c.Property(p => p.CollectedOn).HasInstant("cod_collected_on").IsRequired();
            c.Property(p => p.RecordedBy).HasColumnName("cod_recorded_by").IsRequired();
        });

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("order_line", OrderingMapping.Schema);
            line.WithOwner().HasForeignKey("order_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.LineNumber).HasColumnName("line_number").IsRequired();
            line.Property(l => l.SkuId).HasColumnName("sku_id").IsRequired();
            line.Property(l => l.ProductId).HasColumnName("product_id").IsRequired();
            line.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            line.Property(l => l.VariantLabel).HasColumnName("variant_label").HasMaxLength(200).IsRequired();
            line.Property(l => l.Barcode).HasColumnName("barcode").HasMaxLength(32);
            line.Property(l => l.ReturnClass).HasEnumeration("return_class");
            line.Property(l => l.IsBundle).HasColumnName("is_bundle").IsRequired();
            line.Property(l => l.CatId).HasColumnName("cat_id");
            line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
            line.Property(l => l.UnitPrice).HasMoney("unit_price").IsRequired();
            line.Property(l => l.LineSubtotal).HasMoney("line_subtotal").IsRequired();
            line.Property(l => l.LineDiscount).HasMoney("line_discount").IsRequired();
            line.Property(l => l.OrderDiscountShare).HasMoney("order_discount_share").IsRequired();
            line.Property(l => l.TaxClassId).HasColumnName("tax_class_id").IsRequired();
            line.Property(l => l.TaxRatePercent).HasColumnName("tax_rate_percent").HasColumnType("numeric(5,2)").IsRequired();
            line.Property(l => l.TaxAmount).HasMoney("tax_amount").IsRequired();
            line.Property(l => l.LineTotal).HasMoney("line_total").IsRequired();
            line.Property(l => l.QuantityReturned).HasColumnName("quantity_returned").IsRequired();
            line.Property(l => l.Components).HasJsonbList("components");

            line.OwnsMany(l => l.Allocations, allocation =>
            {
                allocation.ToTable("order_line_allocation", OrderingMapping.Schema);
                allocation.WithOwner().HasForeignKey("order_line_id");
                allocation.HasKey(a => a.Id);
                allocation.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
                allocation.Property(a => a.SkuId).HasColumnName("sku_id").IsRequired();
                allocation.Property(a => a.BatchId).HasColumnName("batch_id").IsRequired();
                allocation.Property(a => a.LotNumber).HasColumnName("lot_number").HasMaxLength(60).IsRequired();
                allocation.Property(a => a.LocationId).HasColumnName("location_id").IsRequired();
                allocation.Property(a => a.Quantity).HasColumnName("quantity").HasColumnType("numeric(14,3)").IsRequired();
                allocation.Property(a => a.BatchExpiresOn).HasColumnName("batch_expires_on").HasColumnType("date");
                allocation.HasIndex(a => a.BatchId).HasDatabaseName("ix_order_line_allocation_batch");
            });
            line.Navigation(l => l.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

            line.HasIndex("order_id").HasDatabaseName("ix_order_line_order");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.StatusHistory, history =>
        {
            history.ToTable("order_status_change", OrderingMapping.Schema);
            history.WithOwner().HasForeignKey("order_id");
            history.HasKey(h => h.Id);
            history.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
            history.Property(h => h.From).HasNullableEnumeration("from_status");
            history.Property(h => h.To).HasEnumeration("to_status");
            history.Property(h => h.ChangedOn).HasInstant("changed_on").IsRequired();
            history.Property(h => h.ChangedBy).HasColumnName("changed_by").IsRequired();
            history.Property(h => h.Note).HasColumnName("note").HasMaxLength(OrderRules.MaxStatusNoteLength);
            history.HasIndex("order_id").HasDatabaseName("ix_order_status_change_order");
        });
        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Payments, payment =>
        {
            payment.ToTable("order_payment_ref", OrderingMapping.Schema);
            payment.WithOwner().HasForeignKey("order_id");
            payment.HasKey(p => p.Id);
            payment.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            payment.Property(p => p.PaymentId).HasColumnName("payment_id").IsRequired();
            payment.Property(p => p.Method).HasColumnName("method").HasMaxLength(30).IsRequired();
            payment.Property(p => p.Amount).HasMoney("amount").IsRequired();
            payment.Property(p => p.RecordedOn).HasInstant("recorded_on").IsRequired();
            payment.HasIndex("order_id").HasDatabaseName("ix_order_payment_ref_order");
        });
        builder.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("ix_sales_order_number");
        builder.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique().HasDatabaseName("ix_sales_order_customer_idempotency")
            .HasFilter("idempotency_key IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.PlacedOn }).HasDatabaseName("ix_sales_order_customer_placed");
        builder.HasIndex(x => new { x.BranchId, x.Status, x.PlacedOn }).HasDatabaseName("ix_sales_order_queue");
        builder.HasIndex(x => new { x.Status, x.PaymentMethod, x.PaymentDueBy }).HasDatabaseName("ix_sales_order_expiry");
        builder.HasIndex(x => new { x.DiscountCodeId, x.CustomerId }).HasDatabaseName("ix_sales_order_discount_customer").HasFilter("discount_code_id IS NOT NULL");
    }
}
