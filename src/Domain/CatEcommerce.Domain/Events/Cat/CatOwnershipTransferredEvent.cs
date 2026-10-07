using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

/// <summary>Carries the actor (via inherited UserContext) and the reason — audited, US-CAT-08 AC1.</summary>
public sealed class CatOwnershipTransferredEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid PreviousOwnerId { get; init; }
    public required Guid NewOwnerId { get; init; }
    public required string Reason { get; init; }
}
