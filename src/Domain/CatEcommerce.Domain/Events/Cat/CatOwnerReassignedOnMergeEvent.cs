using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Cat;

/// <summary>
/// HO-ACC-001 §8.9. Raised by <c>CatAggregate.ReassignOwnerOnCustomerMerge</c> -- deliberately not
/// <c>CatOwnershipTransferredEvent</c>: a merge is not a change of person, and a future
/// "notify the new owner" subscriber to that event must not fire on one.
/// </summary>
public sealed class CatOwnerReassignedOnMergeEvent : Event
{
    public required Guid CatId { get; init; }
    public required Guid PreviousOwnerId { get; init; }
    public required Guid NewOwnerId { get; init; }
}
