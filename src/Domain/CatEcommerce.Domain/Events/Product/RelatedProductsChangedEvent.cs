using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

/// <summary>HO-SHP-001 section 11.5.</summary>
public sealed class RelatedProductsChangedEvent : Event
{
    public required Guid ProductId { get; init; }
}
