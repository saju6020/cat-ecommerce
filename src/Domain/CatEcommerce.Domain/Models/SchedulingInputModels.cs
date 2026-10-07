using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-APT-001 §8.12 -- input/output of <c>IAppointmentSchedulingInputs</c>.</summary>
public sealed record AppointmentSchedulingInputRequest(
    Guid BranchId,
    Guid ServiceId,
    DateOnly LocalDate,
    bool RestrictToOpeningHours,
    Guid? ExcludeAppointmentId,
    Guid? CatId,
    Guid? PreferredPerformerId);

/// <summary><paramref name="Price"/> is <c>null</c> when the service has no price in force --
/// "priced" (HO-ADM-001 <c>IsPriced</c>) is <c>Price is not null</c>.</summary>
public sealed record SchedulingServiceInfo(
    string Name,
    ServiceCategory Category,
    int DurationMinutes,
    IReadOnlyList<string> QualifiedRoleIds,
    RoomKind RequiredRoomKind,
    bool IsActive,
    bool IsBookableOnline,
    Money? Price);

public sealed record AppointmentSchedulingInputSet(
    string TimeZoneId,
    SchedulingServiceInfo? Service,
    IReadOnlyList<SlotPerformerInput> Performers,
    IReadOnlyList<SlotRoomInput> Rooms,
    SlotBusyIntervals Busy);
