using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class ContentItemCreatedEvent : Event
{
    public required Guid ContentItemId { get; init; }
}

public sealed class ContentItemDraftEditedEvent : Event
{
    public required Guid ContentItemId { get; init; }
}

public sealed class ContentItemPublishedEvent : Event
{
    public required Guid ContentItemId { get; init; }
}

public sealed class ContentItemUnpublishedEvent : Event
{
    public required Guid ContentItemId { get; init; }
}
