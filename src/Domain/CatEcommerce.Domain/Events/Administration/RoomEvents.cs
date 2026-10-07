using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class RoomCreatedEvent : Event
{
    public required Guid RoomId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class RoomUpdatedEvent : Event
{
    public required Guid RoomId { get; init; }
}
