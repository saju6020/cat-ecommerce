using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-BIL-001 §8.3 / §12.1 -- <c>billing.charge</c>. The unique <c>ix_charge_source</c> on
/// <c>(source_kind, source_id, source_line_ref)</c> is the idempotency key of both the push and pull paths
/// (ADR-178); the unit price is a <b>snapshot</b> -- never re-read (HO-ADM-001 ADR-049). Whether a charge is
/// billed is derived from the invoice lines (the filtered unique index on <c>invoice_line.charge_id</c>), not stored here.
/// </summary>
public sealed class ChargeConfiguration : IEntityTypeConfiguration<ChargeAggregate>
{
    public void Configure(EntityTypeBuilder<ChargeAggregate> builder)
    {
        builder.ToTable("charge", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id");
        builder.Property(x => x.BusinessLine).HasColumnName("business_line").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.ItemKind).HasColumnName("item_kind").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.SourceKind).HasColumnName("source_kind").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(x => x.SourceLineRef).HasColumnName("source_line_ref").HasMaxLength(100).IsRequired();
        builder.Property(x => x.BillingGroupKind).HasColumnName("billing_group_kind").HasNullableEnumerationConversion();
        builder.Property(x => x.BillingGroupId).HasColumnName("billing_group_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,3)").IsRequired();
        builder.Property(x => x.UnitPriceAmount).HasColumnName("unit_price_amount").HasMoneyColumn().IsRequired();
        builder.Property(x => x.LineDiscountAmount).HasColumnName("line_discount_amount").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.ServiceDate).HasColumnName("service_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.ReportingTag).HasColumnName("reporting_tag").HasMaxLength(50);
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by").IsRequired();
        builder.Property(x => x.VoidedOn).HasColumnName("voided_on").HasColumnType("timestamptz");
        builder.Property(x => x.VoidedBy).HasColumnName("voided_by");
        builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasMaxLength(500);

        builder.Ignore(x => x.UnitPrice);
        builder.Ignore(x => x.LineDiscount);

        builder.HasIndex(x => new { x.SourceKind, x.SourceId, x.SourceLineRef }).IsUnique().HasDatabaseName("ix_charge_source");
        builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("ix_charge_customer_status");
        builder.HasIndex(x => new { x.BillingGroupKind, x.BillingGroupId, x.ServiceDate }).HasDatabaseName("ix_charge_billing_group");
        builder.HasIndex(x => new { x.BranchId, x.ServiceDate }).HasDatabaseName("ix_charge_branch_service_date");

        builder.ConfigureRootTail();
    }
}
