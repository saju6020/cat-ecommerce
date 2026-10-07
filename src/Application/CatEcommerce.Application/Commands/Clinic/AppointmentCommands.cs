using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Clinic;

/// <summary>HO-APT-001 §11.1. Ids are minted in the controller; <c>ValidateCommand = true</c>
/// everywhere. Data only -- no orchestration.</summary>
public sealed class PlaceSlotHoldCommand : Command
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required Guid CatId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public Guid? PerformerId { get; init; }
}

public sealed class ReleaseSlotHoldCommand : Command
{
    public required Guid AppointmentId { get; init; }
}

public sealed class ConfirmAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    public string? ReasonForVisit { get; init; }
}

public sealed class RescheduleAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public Guid? PerformerId { get; init; }
}

public sealed class CancelAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }

    /// <summary>True when the resolved caller is staff (set by the controller from
    /// <c>ResolvedCaller.Kind</c>; backlog #33). Staff may cancel any appointment of their active
    /// branch; a customer only their own.</summary>
    public bool IsStaff { get; init; }

    /// <summary>The staff caller's active branch (§8.3); required when <see cref="IsStaff"/>.</summary>
    public Guid? BranchId { get; init; }
    public string? Reason { get; init; }
}

public sealed class BookAppointmentByStaffCommand : Command
{
    public required Guid AppointmentId { get; init; }

    /// <summary>Minted by the controller from <c>IActiveBranchAccessor</c> (§8.3), not the caller
    /// -- HO-ADM-001 §8.2's branch-scoping rule (§11.4).</summary>
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid PerformerId { get; init; }
    public Guid? RoomId { get; init; }
    public string? ReasonForVisit { get; init; }
}

public sealed class MoveAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid PerformerId { get; init; }
    public required Guid RoomId { get; init; }
}

public sealed class RegisterWalkInCommand : Command
{
    public required Guid AppointmentId { get; init; }

    /// <summary>Minted by the controller from <c>IActiveBranchAccessor</c>, not the caller.</summary>
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required string TriagePriority { get; init; }
    public string? PresentingComplaint { get; init; }
}

public sealed class CheckInAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
    public string? TriagePriority { get; init; }
}

public sealed class SetTriagePriorityCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
    public required string TriagePriority { get; init; }
}

public sealed class StartAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
    public Guid? PerformerId { get; init; }
    public Guid? RoomId { get; init; }
}

public sealed class CompleteAppointmentCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
}

public sealed class RecordNoShowCommand : Command
{
    public required Guid AppointmentId { get; init; }
    /// <summary>The caller's active branch, minted by the controller from <c>IActiveBranchAccessor</c>
    /// (HO-ADM-001 §8.2/§8.3). An appointment of another branch is "not found" (backlog #8).</summary>
    public required Guid BranchId { get; init; }
}
