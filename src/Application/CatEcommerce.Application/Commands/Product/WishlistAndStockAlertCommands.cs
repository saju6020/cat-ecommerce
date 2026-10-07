using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C14 -- the customer is the caller (<c>UserContext.UserId</c>);
/// there is no customer id a client can send.</summary>
public sealed class AddWishlistItemCommand : Command
{
    public required Guid SkuId { get; init; }
}

public sealed class RemoveWishlistItemCommand : Command
{
    public required Guid SkuId { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C15.</summary>
public sealed class RequestStockAlertCommand : Command
{
    public required Guid StockAlertId { get; init; }
    public required Guid SkuId { get; init; }
}

public sealed class CancelStockAlertCommand : Command
{
    public required Guid StockAlertId { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C16 -- sent only by <c>ShopWorker</c>, never over HTTP.</summary>
public sealed class NotifyStockAlertCommand : Command
{
    public required Guid StockAlertId { get; init; }
}
