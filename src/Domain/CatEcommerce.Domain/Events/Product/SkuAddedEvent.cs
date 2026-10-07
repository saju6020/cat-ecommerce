using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5 -- a contract other areas may subscribe to.</summary>
public sealed class SkuAddedEvent : Event
{
    public required Guid ProductId { get; init; }
    public required Guid SkuId { get; init; }
}
