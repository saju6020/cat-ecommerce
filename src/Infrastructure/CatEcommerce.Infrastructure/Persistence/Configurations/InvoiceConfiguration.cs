using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-BIL-001 §8.6 / §12.1 -- <c>billing.invoice</c> and its owned <c>invoice_line</c>. The number and the
/// <c>(branch, financial year, sequence)</c> triple are unique <b>backstops</b> (filtered to issued rows); the
/// filtered unique <c>ix_invoice_line_charge</c> is what makes "a charge is billed at most once" true under
/// concurrency without mutating the charge (ADR-178). The immutability triggers are hand-written SQL in the
/// <c>AddBillingInvoices</c> migration (ADR-180).
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<InvoiceAggregate>
{
    public void Configure(EntityTypeBuilder<InvoiceAggregate> builder)
    {
        builder.ToTable("invoice", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationConversion().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(40);
        builder.Property(x => x.FinancialYear).HasColumnName("financial_year").HasMaxLength(10);
        builder.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz");
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").HasColumnType("date");
        builder.Property(x => x.DueOn).HasColumnName("due_on").HasColumnType("date");
        builder.Property(x => x.NetTotal).HasColumnName("net_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.DiscountTotal).HasColumnName("discount_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.TaxTotal).HasColumnName("tax_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.PaidTotal).HasColumnName("paid_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CreditedTotal).HasColumnName("credited_total").HasMoneyColumn().IsRequired();
        builder.Property(x => x.CorrectsInvoiceId).HasColumnName("corrects_invoice_id");
        builder.Property(x => x.CorrectsCreditNoteId).HasColumnName("corrects_credit_note_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.DraftedBy).HasColumnName("drafted_by").IsRequired();
        builder.Property(x => x.IssuedBy).HasColumnName("issued_by");
        builder.Property(x => x.IssueIdempotencyKey).HasColumnName("issue_idempotency_key").HasMaxLength(100);

        builder.OwnsOne(x => x.Seller, seller =>
        {
            seller.Property(s => s.LegalName).HasColumnName("seller_legal_name").HasMaxLength(200).IsRequired();
            seller.Property(s => s.TaxRegistrationNumber).HasColumnName("seller_tax_registration_number").HasMaxLength(50).IsRequired();
            seller.Property(s => s.AddressText).HasColumnName("seller_address_text").HasMaxLength(1100).IsRequired();
            seller.Property(s => s.Phone).HasColumnName("seller_phone").HasMaxLength(30);
            seller.Property(s => s.Email).HasColumnName("seller_email").HasMaxLength(200);
            seller.Property(s => s.InvoiceFooter).HasColumnName("seller_invoice_footer").HasMaxLength(500);
            seller.Property(s => s.BankAccountsJson).HasColumnName("seller_bank_accounts_json").IsRequired();
        });

        builder.OwnsOne(x => x.BillTo, billTo =>
        {
            billTo.Property(b => b.FullName).HasColumnName("bill_to_full_name").HasMaxLength(200).IsRequired();
            billTo.Property(b => b.Mobile).HasColumnName("bill_to_mobile").HasMaxLength(30);
            billTo.Property(b => b.Email).HasColumnName("bill_to_email").HasMaxLength(200);
            billTo.Property(b => b.AddressText).HasColumnName("bill_to_address_text").HasMaxLength(600);
        });

        builder.OwnsOne(x => x.LoyaltyRedemption, redemption =>
        {
            redemption.Property(r => r.Points).HasColumnName("loyalty_points").IsRequired();
            redemption.Property(r => r.Value).HasColumnName("loyalty_value").HasMoneyColumn().IsRequired();
            redemption.Property(r => r.LoyaltyEntryId).HasColumnName("loyalty_entry_id").IsRequired();
        });

        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("invoice_line", "billing");
            line.WithOwner().HasForeignKey("invoice_id");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            line.Property(l => l.ChargeId).HasColumnName("charge_id");
            line.Property(l => l.CorrectsInvoiceLineId).HasColumnName("corrects_invoice_line_id");
            line.Property(l => l.BusinessLine).HasColumnName("business_line").HasEnumerationConversion().IsRequired();
            line.Property(l => l.ItemKind).HasColumnName("item_kind").HasEnumerationConversion().IsRequired();
            line.Property(l => l.SourceKind).HasColumnName("source_kind").HasEnumerationConversion().IsRequired();
            line.Property(l => l.SourceId).HasColumnName("source_id").IsRequired();
            line.Property(l => l.BillingGroupKind).HasColumnName("billing_group_kind").HasNullableEnumerationConversion();
            line.Property(l => l.BillingGroupId).HasColumnName("billing_group_id");
            line.Property(l => l.CatId).HasColumnName("cat_id");
            line.Property(l => l.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
            line.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("numeric(10,3)").IsRequired();
            line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasMoneyColumn().IsRequired();
            line.Property(l => l.LineDiscount).HasColumnName("line_discount").HasMoneyColumn().IsRequired();
            line.Property(l => l.InvoiceDiscountShare).HasColumnName("invoice_discount_share").HasMoneyColumn().IsRequired();
            line.Property(l => l.TaxClassId).HasColumnName("tax_class_id").IsRequired();
            line.Property(l => l.TaxClassCode).HasColumnName("tax_class_code").HasMaxLength(30);
            line.Property(l => l.TaxRatePercent).HasColumnName("tax_rate_percent").HasColumnType("numeric(5,2)").IsRequired();
            line.Property(l => l.NetAmount).HasColumnName("net_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.TaxAmount).HasColumnName("tax_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.GrossAmount).HasColumnName("gross_amount").HasMoneyColumn().IsRequired();
            line.Property(l => l.SortOrder).HasColumnName("sort_order").IsRequired();
            line.Property(l => l.InvoiceDiscarded).HasColumnName("invoice_discarded").IsRequired();
            line.Property(l => l.IsFrozen).HasColumnName("is_frozen").IsRequired();

            line.HasIndex(l => l.ChargeId).IsUnique().HasDatabaseName("ix_invoice_line_charge")
                .HasFilter("charge_id IS NOT NULL AND NOT invoice_discarded");
        });
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.Outstanding);

        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ix_invoice_number").HasFilter("number IS NOT NULL");
        builder.HasIndex(x => new { x.BranchId, x.FinancialYear, x.SequenceNumber }).IsUnique().HasDatabaseName("ix_invoice_sequence")
            .HasFilter("sequence_number IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("ix_invoice_customer_status");
        builder.HasIndex(x => new { x.BranchId, x.IssueDate }).HasDatabaseName("ix_invoice_branch_issue_date");
        builder.HasIndex(x => new { x.Status, x.DueOn }).HasDatabaseName("ix_invoice_status_due_on");
        builder.HasIndex(x => x.CorrectsInvoiceId).HasDatabaseName("ix_invoice_corrects_invoice");
        builder.HasIndex(x => new { x.IssuedBy, x.IssueIdempotencyKey }).IsUnique().HasDatabaseName("ix_invoice_idempotency")
            .HasFilter("issue_idempotency_key IS NOT NULL");

        builder.ConfigureRootTail();
    }
}
