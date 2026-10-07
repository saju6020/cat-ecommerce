using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5 -- a contract other areas may subscribe to (Orders/Inventory
/// re-read <c>GetBundleCompositionQuery</c>). Ids only (U-X1).</summary>
public sealed class BundleCompositionChangedEvent : Event
{
    public required Guid ProductId { get; init; }
}
