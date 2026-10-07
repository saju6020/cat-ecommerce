using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatRegisteredEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid OwnerId { get; init; }
    public required string Name { get; init; }
}
