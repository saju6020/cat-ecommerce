using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-ORD-001 §11.3 -- command to domain DTO for discount codes. Dates are normalised to UTC before they reach the aggregate.</summary>
public static class DiscountCodeMapper
{
    public static DiscountDefinitionDto ToDomainDto(this CreateDiscountCodeCommand c, string currencyCode) =>
        new(c.Code, c.Description, DeliveryZoneMapper.ParseEnum<DiscountKind>(c.Kind), c.Percent, c.Amount, currencyCode, c.MinimumSpend,
            DeliveryZoneMapper.ParseEnum<DiscountScope>(c.Scope), c.ProductIds, c.CategoryIds, c.FirstOrderOnly, c.UsageLimit, c.PerCustomerLimit,
            c.ValidFrom?.ToUniversalTime(), c.ValidTo?.ToUniversalTime(), c.IsActive, c.UserContext!, c.CorrelationId);

    /// <summary>An update keeps the stored code text: the command never carries one.</summary>
    public static DiscountDefinitionDto ToDomainDto(this UpdateDiscountCodeCommand c, string storedCode, string currencyCode) =>
        new(storedCode, c.Description, DeliveryZoneMapper.ParseEnum<DiscountKind>(c.Kind), c.Percent, c.Amount, currencyCode, c.MinimumSpend,
            DeliveryZoneMapper.ParseEnum<DiscountScope>(c.Scope), c.ProductIds, c.CategoryIds, c.FirstOrderOnly, c.UsageLimit, c.PerCustomerLimit,
            c.ValidFrom?.ToUniversalTime(), c.ValidTo?.ToUniversalTime(), c.IsActive, c.UserContext!, c.CorrelationId);
}
