using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

public sealed class GetDeliveryZonesQuery : Query<IReadOnlyList<DeliveryZoneSummaryViewModel>>;

public sealed class GetDeliveryZoneQuery : Query<DeliveryZoneViewModel?>
{
    public required Guid ZoneId { get; init; }
}

/// <summary>Which active zone serves an address (US-ORD-06 AC2) -- for the admin's test tool.</summary>
public sealed class ResolveDeliveryZoneQuery : Query<ResolvedDeliveryZoneViewModel>
{
    public required string District { get; init; }
    public string? PostalCode { get; init; }
}

public sealed class GetCouriersQuery : Query<IReadOnlyList<CourierViewModel>>
{
    public bool ActiveOnly { get; init; }
}

public sealed class GetBankTransferAccountQuery : Query<BankTransferAccountViewModel>;

public sealed class GetDiscountCodesQuery : Query<IReadOnlyList<DiscountCodeViewModel>>;
