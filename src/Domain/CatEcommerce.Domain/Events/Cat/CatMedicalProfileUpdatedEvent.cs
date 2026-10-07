using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatMedicalProfileUpdatedEvent : Event
{
    public required Guid CatId { get; init; }
}
