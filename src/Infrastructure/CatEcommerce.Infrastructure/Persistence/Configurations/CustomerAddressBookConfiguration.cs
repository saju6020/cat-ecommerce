using CatEcommerce.Domain.Aggregates.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ACC-001 §12.1. Id is the customer id -- a 1:1 shared key (ADR-035).</summary>
public sealed class CustomerAddressBookConfiguration : IEntityTypeConfiguration<CustomerAddressBookAggregate>
{
    public void Configure(EntityTypeBuilder<CustomerAddressBookAggregate> builder)
    {
        builder.ToTable("customer_address_book", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.DefaultAddressId).HasColumnName("default_address_id");

        builder.OwnsMany(x => x.Addresses, address =>
        {
            address.ToTable("customer_delivery_address", "account");
            address.WithOwner().HasForeignKey("customer_id");
            address.HasKey("Id");
            address.Property(a => a.Id).ValueGeneratedNever();
            address.Property(a => a.Label).HasMaxLength(50).IsRequired();
            address.Property(a => a.Line1).HasMaxLength(200).IsRequired();
            address.Property(a => a.Line2).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100).IsRequired();
            address.Property(a => a.District).HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(20);
            address.Property(a => a.DeliveryInstructions).HasMaxLength(500);
            address.Property(a => a.AddedOn).HasColumnType("timestamptz");
            address.Property(a => a.LastChangedOn).HasColumnType("timestamptz");
        });
        builder.Navigation(x => x.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
