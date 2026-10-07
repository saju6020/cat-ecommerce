using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class ProductFoodDetailsUpdatedEvent : Event
{
    public required Guid ProductId { get; init; }
}
