using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettingsAggregate>
{
    public void Configure(EntityTypeBuilder<BusinessSettingsAggregate> builder)
    {
        builder.ToTable("business_settings", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.IsGlobalScope);

        builder.OwnsMany(x => x.Values, v =>
        {
            v.ToTable("business_settings_value", "admin");
            v.WithOwner().HasForeignKey("business_settings_id");
            v.HasKey("business_settings_id", nameof(CatEcommerce.Domain.Entities.SettingValue.Key));
            v.Property(p => p.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
            v.Property(p => p.Value).HasColumnName("value").HasMaxLength(500).IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(BusinessSettingsAggregate.Values))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
