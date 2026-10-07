namespace CatEcommerce.Api.Contracts.Clinic;

/// <summary>HO-APT-001 §12.4 request bodies.</summary>
public sealed class PlaceSlotHoldRequest
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required Guid CatId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public Guid? PerformerId { get; init; }
}

public sealed class ConfirmAppointmentRequest
{
    public string? ReasonForVisit { get; init; }
}

public sealed class RescheduleAppointmentRequest
{
    public required DateTimeOffset StartsOn { get; init; }
    public Guid? PerformerId { get; init; }
}

public sealed class CancelAppointmentRequest
{
    public string? Reason { get; init; }
}

public sealed class JoinWaitlistRequest
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required Guid CatId { get; init; }
    public required DateOnly LocalDate { get; init; }
    public Guid? PreferredPerformerId { get; init; }
}

public sealed class BookAppointmentRequest
{
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid PerformerId { get; init; }
    public Guid? RoomId { get; init; }
    public string? ReasonForVisit { get; init; }
}

public sealed class MoveAppointmentRequest
{
    public required int ExpectedVersion { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid PerformerId { get; init; }
    public required Guid RoomId { get; init; }
}

public sealed class RegisterWalkInRequest
{
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required string TriagePriority { get; init; }
    public string? PresentingComplaint { get; init; }
}

public sealed class CheckInRequest
{
    public string? TriagePriority { get; init; }
}

public sealed class SetTriagePriorityRequest
{
    public required string TriagePriority { get; init; }
}

public sealed class StartAppointmentRequest
{
    public Guid? PerformerId { get; init; }
    public Guid? RoomId { get; init; }
}
