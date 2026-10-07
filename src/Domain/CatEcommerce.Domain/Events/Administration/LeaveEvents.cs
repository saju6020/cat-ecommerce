using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class LeaveRequestedEvent : Event
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid StaffAccountId { get; init; }
}

/// <summary>ADR-048 -- an integration contract. HO-APT-001 and HO-BRD-001 subscribe to flag
/// affected appointments/tasks. No note, no reason -- ids, dates only.</summary>
public sealed class StaffLeaveApprovedEvent : Event
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
}

public sealed class LeaveRejectedEvent : Event
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid StaffAccountId { get; init; }
}

/// <summary>The matching un-flag contract for <see cref="StaffLeaveApprovedEvent"/>.</summary>
public sealed class LeaveCancelledEvent : Event
{
    public required Guid LeaveRequestId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required bool WasApproved { get; init; }
}
