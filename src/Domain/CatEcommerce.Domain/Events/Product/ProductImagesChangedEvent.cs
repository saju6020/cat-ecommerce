using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class ProductImagesChangedEvent : Event
{
    public required Guid ProductId { get; init; }
}
