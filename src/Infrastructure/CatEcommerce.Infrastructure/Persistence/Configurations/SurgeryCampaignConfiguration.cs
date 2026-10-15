using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-SRG-001 section 8.11 / 12.1 (ADR-171) -- <c>clinic.surgery_campaign</c> (<c>ix_surgery_campaign_code</c> UNIQUE) and its owned per-service prices. Practice configuration
/// (Operational): deactivated, never deleted. The branch list is a uuid[] (empty = every branch). No foreign key to a service or branch (ADR-011).
/// </summary>
public sealed class SurgeryCampaignConfiguration : IEntityTypeConfiguration<SurgeryCampaignAggregate>
{
    public void Configure(EntityTypeBuilder<SurgeryCampaignAggregate> builder)
    {
        builder.ToTable("surgery_campaign", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(SurgeryRules.CampaignCodeMax).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(SurgeryRules.CampaignNameMax).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(SurgeryRules.CampaignDescriptionMax).IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").HasColumnType("date").IsRequired();
        builder.Property(x => x.MaxCases).HasColumnName("max_cases");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.PrimitiveCollection(x => x.BranchIds).HasColumnName("branch_ids").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Prices, p =>
        {
            p.ToTable("surgery_campaign_price", "clinic");
            p.WithOwner().HasForeignKey("campaign_id");
            p.HasKey("Id");
            p.Property(v => v.Id).ValueGeneratedNever().HasColumnName("id");
            p.Property(v => v.ServiceId).HasColumnName("service_id").IsRequired();
            p.OwnsOne(v => v.Price, money =>
            {
                money.Property(m => m.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
                money.Property(m => m.CurrencyCode).HasColumnName("price_currency_code").HasColumnType("char(3)").IsRequired();
            });
            p.Navigation(v => v.Price).IsRequired();
            p.HasIndex("campaign_id", nameof(CatEcommerce.Domain.Entities.CampaignServicePrice.ServiceId)).IsUnique().HasDatabaseName("ix_surgery_campaign_price_campaign_service");
        });
        builder.Metadata.FindNavigation(nameof(SurgeryCampaignAggregate.Prices))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_surgery_campaign_code");
    }
}
