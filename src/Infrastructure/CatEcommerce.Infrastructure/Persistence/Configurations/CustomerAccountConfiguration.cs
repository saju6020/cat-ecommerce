using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ACC-001 §12.1. Schema <c>account</c>.</summary>
public sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccountAggregate>
{
    public void Configure(EntityTypeBuilder<CustomerAccountAggregate> builder)
    {
        builder.ToTable("customer_account", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();

        builder.OwnsOne(x => x.MobileNumber, m =>
        {
            m.Property(p => p.Value).HasColumnName("mobile_number").HasMaxLength(20);
            m.HasIndex(p => p.Value).IsUnique().HasDatabaseName("ix_customer_account_mobile_number")
                .HasFilter("mobile_number IS NOT NULL");
        });
        builder.Navigation(x => x.MobileNumber).IsRequired(false);

        builder.OwnsOne(x => x.EmailAddress, e =>
        {
            e.Property(p => p.Value).HasColumnName("email_address").HasMaxLength(256);
            e.HasIndex(p => p.Value).IsUnique().HasDatabaseName("ix_customer_account_email_address")
                .HasFilter("email_address IS NOT NULL");
        });
        builder.Navigation(x => x.EmailAddress).IsRequired(false);

        builder.OwnsOne(x => x.PasswordCredential, p =>
        {
            p.Property(c => c.Hash).HasColumnName("password_hash").HasMaxLength(256);
            p.Property(c => c.Algorithm).HasColumnName("password_algorithm").HasMaxLength(50);
            p.Property(c => c.SetOn).HasColumnName("password_set_on").HasColumnType("timestamptz");
        });
        builder.Navigation(x => x.PasswordCredential).IsRequired(false);

        builder.OwnsOne(x => x.LoginThrottle, t =>
        {
            t.Property(p => p.FailedCount).HasColumnName("failed_login_count").IsRequired();
            t.Property(p => p.LockedUntil).HasColumnName("locked_until").HasColumnType("timestamptz");
            t.Property(p => p.LastFailedOn).HasColumnName("last_failed_login_on").HasColumnType("timestamptz");
        });
        builder.Navigation(x => x.LoginThrottle).IsRequired();

        builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp").IsRequired();

        builder.Property(x => x.RegistrationChannel)
            .HasColumnName("registration_channel")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<RegistrationChannel>(v))
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<CustomerAccountStatus>(v))
            .IsRequired();

        builder.Property(x => x.LastSuccessfulLoginOn).HasColumnName("last_successful_login_on").HasColumnType("timestamptz");
        builder.Property(x => x.MergedIntoCustomerId).HasColumnName("merged_into_customer_id");
        builder.Property(x => x.MergedOn).HasColumnName("merged_on").HasColumnType("timestamptz");
        builder.Property(x => x.AnonymisedOn).HasColumnName("anonymised_on").HasColumnType("timestamptz");
        builder.Property(x => x.LastPersonalDataExportOn).HasColumnName("last_personal_data_export_on").HasColumnType("timestamptz");

        builder.PrimitiveCollection(x => x.AdditionalPhoneNumbers)
            .HasColumnName("additional_phone_numbers")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType(e => e.HasMaxLength(20));
        builder.HasIndex(x => x.AdditionalPhoneNumbers)
            .HasDatabaseName("ix_customer_account_additional_phone_numbers")
            .HasMethod("GIN");

        builder.HasIndex(x => x.MergedIntoCustomerId).HasDatabaseName("ix_customer_account_merged_into_customer_id");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
        builder.Ignore(x => x.RecordStatus);
    }
}
