using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class ProductSlugChangedEvent : Event
{
    public required Guid ProductId { get; init; }
}
