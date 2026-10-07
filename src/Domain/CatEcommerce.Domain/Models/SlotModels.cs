using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-APT-001 §8.5 -- inputs and results of <c>AppointmentSlotFinder</c>.</summary>
public sealed record SlotServiceInfo(int DurationMinutes, IReadOnlyList<string> QualifiedRoleIds, RoomKind RequiredRoomKind);

/// <summary>One of a performer's available windows (from HO-ADM-001's <c>StaffAvailability</c>),
/// carrying its own <see cref="OutsideOpeningHours"/> flag for staff-mode requests.</summary>
public sealed record SlotAvailabilityWindow(DateTimeOffset Start, DateTimeOffset End, bool OutsideOpeningHours);

public sealed record SlotPerformerInput(Guid StaffAccountId, string Name, IReadOnlyList<string> RoleIds, IReadOnlyList<SlotAvailabilityWindow> AvailableIntervals);

public sealed record SlotRoomInput(Guid RoomId, string Name, RoomKind Kind, bool IsActive);

/// <summary><paramref name="CatBusyIntervals"/> is scoped to the one cat the request cares about
/// (empty when none is known -- the public listing endpoint without a customer session, §12.4);
/// unlike performers and rooms, a slot request is never about "every cat's" bookings.</summary>
public sealed record SlotBusyIntervals(
    IReadOnlyDictionary<Guid, IReadOnlyList<TimeInterval>> ByPerformer,
    IReadOnlyDictionary<Guid, IReadOnlyList<TimeInterval>> ByRoom,
    IReadOnlyList<TimeInterval> CatBusyIntervals)
{
    public static readonly SlotBusyIntervals Empty = new(
        new Dictionary<Guid, IReadOnlyList<TimeInterval>>(),
        new Dictionary<Guid, IReadOnlyList<TimeInterval>>(),
        []);

    public IReadOnlyList<TimeInterval> ForPerformer(Guid id) => ByPerformer.GetValueOrDefault(id, []);
    public IReadOnlyList<TimeInterval> ForRoom(Guid id) => ByRoom.GetValueOrDefault(id, []);
}

public sealed record SlotRequest(
    SlotServiceInfo Service,
    DateOnly LocalDate,
    string TimeZoneId,
    IReadOnlyList<SlotPerformerInput> Performers,
    IReadOnlyList<SlotRoomInput> Rooms,
    SlotBusyIntervals Busy,
    int GridMinutes,
    DateTimeOffset EarliestStart,
    Guid? PreferredPerformerId,
    Guid? PreferredRoomId = null);

public sealed record Slot(DateTimeOffset StartsOn, DateTimeOffset EndsOn, Guid PerformerId, string PerformerName, Guid RoomId, string RoomName, bool OutsideOpeningHours);
