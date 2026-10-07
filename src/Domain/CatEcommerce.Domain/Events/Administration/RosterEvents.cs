using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class StaffRosterPatternChangedEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required Guid BranchId { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class RosterExceptionCreatedEvent : Event
{
    public required Guid RosterExceptionId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class RosterExceptionCancelledEvent : Event
{
    public required Guid RosterExceptionId { get; init; }
}
