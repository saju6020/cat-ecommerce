using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatProfileUpdatedEvent : Event
{
    public required Guid CatId { get; init; }
}
