using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class ProductDetailsUpdatedEvent : Event
{
    public required Guid ProductId { get; init; }
}
