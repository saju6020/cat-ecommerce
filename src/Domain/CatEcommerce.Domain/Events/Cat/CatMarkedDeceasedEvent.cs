using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatMarkedDeceasedEvent : Event
{
    public required Guid CatId { get; init; }
    public required DateOnly DeceasedOn { get; init; }
}
