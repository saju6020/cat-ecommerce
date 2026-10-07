using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

public sealed class CatPhotoAddedEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid PhotoId { get; init; }
}
