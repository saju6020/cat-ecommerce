using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 12.4 -- the caller's own wishlist.</summary>
public sealed class GetWishlistQuery : Query<WishlistViewModel>
{
    public required Guid CustomerId { get; init; }
}

public sealed class GetMyStockAlertsQuery : Query<IReadOnlyList<StockAlertViewModel>>
{
    public required Guid CustomerId { get; init; }
}

/// <summary>HO-SHP-001 section 8.11 -- what would happen to a <c>shop.back-in-stock</c> message for
/// this customer, per HO-NTF-001's routing policy. Null when the customer cannot be resolved.</summary>
public sealed class GetStockAlertDeliverabilityQuery : Query<StockAlertDeliverabilityViewModel?>
{
    public required Guid CustomerId { get; init; }
}

/// <summary>HO-SHP-001 section 12.2 -- the worker's input: Pending alerts whose SKU is now InStock,
/// oldest first, at most <see cref="Limit"/> per pass.</summary>
public sealed class GetDueStockAlertsQuery : Query<IReadOnlyList<Guid>>
{
    public int Limit { get; init; } = 500;
}
