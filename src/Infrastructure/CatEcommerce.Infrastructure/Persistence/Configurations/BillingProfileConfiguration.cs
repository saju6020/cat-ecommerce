using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BIL-001 §8.1 / §12.1 -- <c>billing.billing_profile</c> (Id == BranchId) and its owned
/// bank accounts (≤ 5, replaced wholesale on every set).</summary>
public sealed class BillingProfileConfiguration : IEntityTypeConfiguration<BillingProfileAggregate>
{
    public void Configure(EntityTypeBuilder<BillingProfileAggregate> builder)
    {
        builder.ToTable("billing_profile", "billing");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaxRegistrationNumber).HasColumnName("tax_registration_number").HasMaxLength(50).IsRequired();
        builder.PrimitiveCollection(x => x.AddressLines).HasColumnName("address_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(x => x.InvoiceFooter).HasColumnName("invoice_footer").HasMaxLength(500);

        builder.OwnsMany(x => x.BankAccounts, account =>
        {
            account.ToTable("billing_profile_bank_account", "billing");
            account.WithOwner().HasForeignKey("billing_profile_id");
            account.HasKey("Id");
            account.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            account.Property(a => a.BankName).HasColumnName("bank_name").HasMaxLength(100).IsRequired();
            account.Property(a => a.AccountName).HasColumnName("account_name").HasMaxLength(100).IsRequired();
            account.Property(a => a.AccountNumber).HasColumnName("account_number").HasMaxLength(50).IsRequired();
            account.Property(a => a.BranchName).HasColumnName("branch_name").HasMaxLength(100);
            account.Property(a => a.RoutingNumber).HasColumnName("routing_number").HasMaxLength(30);
            account.Property(a => a.SortOrder).HasColumnName("sort_order").IsRequired();
        });
        builder.Navigation(x => x.BankAccounts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ConfigureRootTail();
    }
}
