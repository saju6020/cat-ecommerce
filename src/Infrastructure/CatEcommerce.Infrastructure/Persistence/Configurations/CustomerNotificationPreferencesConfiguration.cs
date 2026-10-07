using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ACC-001 §12.1. Id is the customer id -- a 1:1 shared key (ADR-035).</summary>
public sealed class CustomerNotificationPreferencesConfiguration : IEntityTypeConfiguration<CustomerNotificationPreferencesAggregate>
{
    public void Configure(EntityTypeBuilder<CustomerNotificationPreferencesAggregate> builder)
    {
        builder.ToTable("customer_notification_preferences", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.OwnsMany(x => x.MarketingConsents, consent =>
        {
            consent.ToTable("customer_marketing_consent", "account");
            consent.WithOwner().HasForeignKey("customer_id");
            consent.HasKey("Id");
            consent.Property(c => c.Id).ValueGeneratedNever();
            consent.Property(c => c.Channel)
                .HasColumnName("channel")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationChannel>(v))
                .IsRequired();
            consent.Property(c => c.IsGranted).HasColumnName("is_granted").IsRequired();
            consent.Property(c => c.RecordedOn).HasColumnName("recorded_on").HasColumnType("timestamptz").IsRequired();
            consent.Property(c => c.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        });
        builder.Navigation(x => x.MarketingConsents).UsePropertyAccessMode(PropertyAccessMode.Field);

        // HO-NTF-001 §8.5/§12.1 amendment.
        builder.Property(x => x.PreferredChannel)
            .HasColumnName("preferred_channel")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationChannel>(v.Value))
            .IsRequired(false);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
