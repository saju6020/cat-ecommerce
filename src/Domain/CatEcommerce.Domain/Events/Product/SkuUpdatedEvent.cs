using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Product;

public sealed class SkuUpdatedEvent : Event
{
    public required Guid ProductId { get; init; }
    public required Guid SkuId { get; init; }
}
