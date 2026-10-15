using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ORD-001 §12.1 (Phase B) -- <c>ordering.delivery_zone</c> with its owned coverage and service-level rows. Zones are edited in
/// place (ADR-206); the unique name is the backstop of the handler's pre-check (<c>ix_delivery_zone_name</c> -> 400
/// <c>DeliveryZoneNameAlreadyExists</c>).</summary>
public sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZoneAggregate>
{
    public void Configure(EntityTypeBuilder<DeliveryZoneAggregate> builder)
    {
        builder.ConfigureOrderingRoot("delivery_zone");

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(DeliveryZoneRules.MaxNameLength).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CodEnabled).HasColumnName("cod_enabled").IsRequired();
        builder.Property(x => x.CodMaxOrderValue).HasMoney("cod_max_order_value");
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();

        builder.OwnsMany(x => x.Coverage, coverage =>
        {
            coverage.ToTable("delivery_zone_coverage", OrderingMapping.Schema);
            coverage.WithOwner().HasForeignKey("delivery_zone_id");
            coverage.HasKey(c => c.Id);
            coverage.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            coverage.Property(c => c.District).HasColumnName("district").HasMaxLength(DeliveryZoneRules.MaxDistrictLength).IsRequired();
            coverage.Property(c => c.PostalCodes).HasJsonbList("postal_codes");
            coverage.HasIndex("delivery_zone_id").HasDatabaseName("ix_delivery_zone_coverage_zone");
        });
        builder.Navigation(x => x.Coverage).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Services, service =>
        {
            service.ToTable("delivery_zone_service", OrderingMapping.Schema);
            service.WithOwner().HasForeignKey("delivery_zone_id");
            service.HasKey(s => s.Id);
            service.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            service.Property(s => s.ServiceLevel).HasEnumeration("service_level");
            service.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
            service.Property(s => s.BaseCharge).HasMoney("base_charge").IsRequired();
            service.Property(s => s.FreeOverOrderValue).HasMoney("free_over_order_value");
            service.Property(s => s.LeadTimeDays).HasColumnName("lead_time_days").IsRequired();
            service.Property(s => s.WeightBands).HasJsonbList("weight_bands");
            service.Property(s => s.SlotWindows).HasJsonbList("slot_windows");
            service.HasIndex("delivery_zone_id", nameof(CatEcommerce.Domain.Entities.ZoneServiceLevel.ServiceLevel)).IsUnique().HasDatabaseName("ix_delivery_zone_service_level");
        });
        builder.Navigation(x => x.Services).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ix_delivery_zone_name");
        builder.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("ix_delivery_zone_branch_active");
    }
}

/// <summary>HO-ORD-001 §12.1 (Phase B) -- <c>ordering.courier</c>; the unique name is <c>ix_courier_name</c> -> 400 <c>CourierNameAlreadyExists</c>.</summary>
public sealed class CourierConfiguration : IEntityTypeConfiguration<CourierAggregate>
{
    public void Configure(EntityTypeBuilder<CourierAggregate> builder)
    {
        builder.ConfigureOrderingRoot("courier");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(OrderRules.MaxNameLength).IsRequired();
        builder.Property(x => x.TrackingUrlTemplate).HasColumnName("tracking_url_template").HasMaxLength(500);
        builder.Property(x => x.SupportsCod).HasColumnName("supports_cod").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ix_courier_name");
    }
}

/// <summary>HO-ORD-001 §12.1 (Phase B) -- <c>ordering.bank_transfer_account</c>, a singleton (one fixed id). Data, never configuration (§20.8).</summary>
public sealed class BankTransferAccountConfiguration : IEntityTypeConfiguration<BankTransferAccountAggregate>
{
    public void Configure(EntityTypeBuilder<BankTransferAccountAggregate> builder)
    {
        builder.ConfigureOrderingRoot("bank_transfer_account");

        builder.Property(x => x.AccountName).HasColumnName("account_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BankName).HasColumnName("bank_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.BranchName).HasColumnName("branch_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RoutingNumber).HasColumnName("routing_number").HasMaxLength(50);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}

/// <summary>HO-ORD-001 §12.1 (Phase B) -- <c>ordering.discount_code</c>. The unique upper-case code is <c>ix_discount_code_code</c> -> 400
/// <c>DiscountCodeAlreadyExists</c>; the row's <c>xmin</c> serialises two placements racing for the last use (ADR-207).</summary>
public sealed class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCodeAggregate>
{
    public void Configure(EntityTypeBuilder<DiscountCodeAggregate> builder)
    {
        builder.ConfigureOrderingRoot("discount_code");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(DiscountCodeRules.MaxCodeLength).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(DiscountCodeRules.MaxDescriptionLength);
        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.Percent).HasColumnName("percent").HasColumnType("numeric(5,2)");
        builder.Property(x => x.Amount).HasMoney("amount");
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.MinimumSpend).HasMoney("minimum_spend");
        builder.Property(x => x.Scope).HasEnumeration("scope");
        builder.Property(x => x.FirstOrderOnly).HasColumnName("first_order_only").IsRequired();
        builder.Property(x => x.UsageLimit).HasColumnName("usage_limit");
        builder.Property(x => x.PerCustomerLimit).HasColumnName("per_customer_limit");
        builder.Property(x => x.ValidFrom).HasInstant("valid_from");
        builder.Property(x => x.ValidTo).HasInstant("valid_to");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.TimesRedeemed).HasColumnName("times_redeemed").IsRequired();

        builder.Property(x => x.ProductIds).HasJsonbList("product_ids");
        builder.Property(x => x.CategoryIds).HasJsonbList("category_ids");

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_discount_code_code");
    }
}
