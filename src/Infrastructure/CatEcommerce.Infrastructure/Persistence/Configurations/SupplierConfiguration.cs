using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.5 / §12.1. <c>ix_supplier_code</c> is the unique, upper-case supplier code; the contact block is owned (same table,
/// required so an all-empty contact still materialises).</summary>
public sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierAggregate>
{
    public void Configure(EntityTypeBuilder<SupplierAggregate> builder)
    {
        builder.ConfigureInventoryRoot("supplier");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_supplier_code");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.LeadTimeDays).HasColumnName("lead_time_days").IsRequired();
        builder.Property(x => x.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(500).IsRequired();
        builder.Property(x => x.PaymentTermDays).HasColumnName("payment_term_days");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.OwnsOne(x => x.Contact, contact =>
        {
            contact.Property(c => c.ContactName).HasColumnName("contact_name").HasMaxLength(200);
            contact.Property(c => c.Phone).HasColumnName("contact_phone").HasMaxLength(30);
            contact.Property(c => c.Email).HasColumnName("contact_email").HasMaxLength(256);
            contact.Property(c => c.Address).HasColumnName("contact_address").HasMaxLength(500);
        });
        builder.Navigation(x => x.Contact).IsRequired();
    }
}
