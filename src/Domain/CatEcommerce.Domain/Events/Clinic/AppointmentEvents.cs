using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Clinic;

/// <summary>HO-APT-001 §11.5. Ids, instants, enums and booleans only -- no reason text, complaint
/// text or names (U-X1).</summary>
public sealed class AppointmentBookedEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required Guid? PerformerId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required BookingChannel Channel { get; init; }
}

public sealed class AppointmentRescheduledEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required DateTimeOffset PreviousStartsOn { get; init; }
}

public sealed class AppointmentCancelledEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool CancelledByCustomer { get; init; }
}

public sealed class AppointmentCheckedInEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class WalkInRegisteredEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class TriagePriorityChangedEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required TriagePriority Priority { get; init; }
}

/// <summary>HO-EMR-001 contract (§4.2).</summary>
public sealed class AppointmentStartedEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid PerformerId { get; init; }
}

/// <summary>HO-EMR-001 and HO-BIL-001 contract (§4.2).</summary>
public sealed class AppointmentCompletedEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class AppointmentNoShowRecordedEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
}

/// <summary>§8.10.</summary>
public sealed class AppointmentFlaggedForRescheduleEvent : Event
{
    public required Guid AppointmentId { get; init; }
    public required RescheduleFlagReason Reason { get; init; }
}
