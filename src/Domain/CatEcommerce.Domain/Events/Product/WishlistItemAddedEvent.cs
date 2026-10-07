using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5 -- ids only (U-X1).</summary>
public sealed class WishlistItemAddedEvent : Event
{
    public required Guid SkuId { get; init; }
    public required Guid ProductId { get; init; }
}
