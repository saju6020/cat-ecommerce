using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-APT-001 §8.5 -- U-B1, U-B2, U-B4, U-B5, U-B6, U-B11, U-B12. Pure, no I/O.</summary>
public sealed class AppointmentSlotFinderTests
{
    private static readonly DateOnly Date = new(2026, 10, 12); // a Monday
    private const string TimeZoneId = "Asia/Dhaka";

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(Date.Year, Date.Month, Date.Day, hour, minute, 0, TimeSpan.FromHours(6)); // Asia/Dhaka is UTC+6, no DST

    private static SlotAvailabilityWindow Window(int fromHour, int toHour) => new(At(fromHour), At(toHour), false);

    private static SlotPerformerInput Performer(string name, IReadOnlyList<string> roles, params SlotAvailabilityWindow[] windows) =>
        new(Guid.NewGuid(), name, roles, windows);

    private static SlotRoomInput Room(string name, RoomKind kind) => new(Guid.NewGuid(), name, kind, true);

    private static SlotRequest Request(
        IReadOnlyList<SlotPerformerInput> performers, IReadOnlyList<SlotRoomInput> rooms, SlotBusyIntervals? busy = null,
        Guid? preferredPerformerId = null, DateTimeOffset? earliestStart = null, int gridMinutes = 15,
        IReadOnlyList<string>? qualifiedRoles = null) =>
        new(new SlotServiceInfo(30, qualifiedRoles ?? [StaffRoles.Vet], RoomKind.Consultation),
            Date, TimeZoneId, performers, rooms, busy ?? SlotBusyIntervals.Empty, gridMinutes,
            earliestStart ?? At(0), preferredPerformerId);

    /// <summary>U-B1 -- vet-only service against a nurse-only roster yields nothing.</summary>
    [Fact]
    public void FindSlots_NoQualifiedPerformer_ReturnsEmpty()
    {
        var nurse = Performer("Nurse Jahan", [StaffRoles.Nurse], Window(9, 17));
        var request = Request([nurse], [Room("Room 1", RoomKind.Consultation)]);

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.Empty(slots);
    }

    /// <summary>U-B2 -- both qualified, sorted by start then performer name.</summary>
    [Fact]
    public void FindSlots_TwoQualifiedPerformers_ReturnsBoth_SortedByStartThenName()
    {
        var vet = Performer("Dr Rahman", [StaffRoles.Vet], Window(9, 10));
        var nurse = Performer("Nurse Akter", [StaffRoles.Nurse, StaffRoles.Vet], Window(9, 10));
        var request = Request([vet, nurse], [Room("Room 1", RoomKind.Consultation)], qualifiedRoles: [StaffRoles.Vet, StaffRoles.Nurse]);

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.Equal(6, slots.Count); // 09:00, 09:15 and 09:30 starts (last ends exactly at 10:00), two performers each
        Assert.All(slots, s => Assert.True(s.StartsOn == At(9) || s.StartsOn == At(9, 15) || s.StartsOn == At(9, 30)));
        var firstGroup = slots.Where(s => s.StartsOn == At(9)).OrderBy(s => s.PerformerName).ToList();
        Assert.Equal("Dr Rahman", firstGroup[0].PerformerName);
        Assert.Equal("Nurse Akter", firstGroup[1].PerformerName);
    }

    /// <summary>U-B4 -- a 30-min service on a 15-min grid, shift 09:00-12:00, with a booking
    /// 10:00-10:30 blocked: 09:00.09:30 offered, none overlapping 10:00-10:30, 11:45 absent (would
    /// end 12:15).</summary>
    [Fact]
    public void FindSlots_ExistingBooking_ExcludesOverlap_AndRespectsShiftEnd()
    {
        var vet = Performer("Dr Rahman", [StaffRoles.Vet], Window(9, 12));
        var busy = new SlotBusyIntervals(
            new Dictionary<Guid, IReadOnlyList<TimeInterval>> { [vet.StaffAccountId] = [new TimeInterval(At(10), At(10, 30))] },
            new Dictionary<Guid, IReadOnlyList<TimeInterval>>(), []);
        var request = Request([vet], [Room("Room 1", RoomKind.Consultation)], busy);

        var slots = AppointmentSlotFinder.FindSlots(request);
        var starts = slots.Select(s => s.StartsOn).ToList();

        Assert.Contains(At(9), starts);
        Assert.Contains(At(9, 30), starts);
        Assert.DoesNotContain(At(10), starts);
        Assert.DoesNotContain(At(10, 15), starts);
        Assert.Contains(At(11, 30), starts); // ends 12:00 -- the last valid start
        Assert.DoesNotContain(At(11, 45), starts); // would end 12:15, beyond the shift
    }

    /// <summary>U-B5 -- the only room of the required kind is busy: no slot, even though the vet is free.</summary>
    [Fact]
    public void FindSlots_OnlyRoomOfKindBusy_ReturnsEmpty()
    {
        var vet = Performer("Dr Rahman", [StaffRoles.Vet], Window(9, 12));
        var room = Room("Room 1", RoomKind.Consultation);
        var busy = new SlotBusyIntervals(
            new Dictionary<Guid, IReadOnlyList<TimeInterval>>(),
            new Dictionary<Guid, IReadOnlyList<TimeInterval>> { [room.RoomId] = [new TimeInterval(At(9), At(12))] }, []);
        var request = Request([vet], [room], busy);

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.Empty(slots);
    }

    /// <summary>U-B6 -- two free rooms of the kind: always the first by name.</summary>
    [Fact]
    public void FindSlots_TwoFreeRooms_AlwaysPicksFirstByName()
    {
        var vet = Performer("Dr Rahman", [StaffRoles.Vet], Window(9, 10));
        var roomB = Room("Room B", RoomKind.Consultation);
        var roomA = Room("Room A", RoomKind.Consultation);
        var request = Request([vet], [roomB, roomA]);

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal("Room A", s.RoomName));
    }

    /// <summary>U-B11 -- a preferred performer restricts results to that performer only.</summary>
    [Fact]
    public void FindSlots_PreferredPerformer_OnlyThatPerformersSlots()
    {
        var vetA = Performer("Dr A", [StaffRoles.Vet], Window(9, 10));
        var vetB = Performer("Dr B", [StaffRoles.Vet], Window(9, 10));
        var request = Request([vetA, vetB], [Room("Room 1", RoomKind.Consultation)], preferredPerformerId: vetB.StaffAccountId);

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(vetB.StaffAccountId, s.PerformerId));
    }

    /// <summary>U-B9 (part) -- minimum notice: a start before EarliestStart is never offered.</summary>
    [Fact]
    public void FindSlots_BeforeEarliestStart_NotOffered()
    {
        var vet = Performer("Dr Rahman", [StaffRoles.Vet], Window(9, 10));
        var request = Request([vet], [Room("Room 1", RoomKind.Consultation)], earliestStart: At(9, 30));

        var slots = AppointmentSlotFinder.FindSlots(request);

        Assert.All(slots, s => Assert.True(s.StartsOn >= At(9, 30)));
        Assert.DoesNotContain(slots, s => s.StartsOn == At(9));
    }

    [Fact]
    public void GridStartsForDay_AlignsToGridFromLocalMidnight()
    {
        var starts = AppointmentSlotFinder.GridStartsForDay(Date, 15, TimeZoneId);

        Assert.Contains(At(9), starts);
        Assert.Contains(At(9, 15), starts);
        Assert.DoesNotContain(At(9, 5), starts);
    }

    /// <summary>U-B10 -- Europe/London, 2026-03-29 (the UK's spring-forward day: clocks jump from
    /// 01:00 to 02:00, so wall-clock 01:00 never occurs). The raw hourly grid still walks all 24
    /// nominal wall-clock hours, but the invalid 01:00 and the following 02:00 both resolve to the
    /// same UTC instant (01:00Z) -- a duplicate, not a gap, and exactly the one hour the real change
    /// affects. <c>AppointmentSlotFinder.FindSlots</c>'s own <c>DistinctBy</c> (§8.5 rule 8) is what
    /// turns that duplicate into "no duplicated ... slots" for a caller.</summary>
    [Fact]
    public void GridStartsForDay_SpringForwardDstDay_OnlyTheChangedHourCollapses()
    {
        var dstDate = new DateOnly(2026, 3, 29);

        var starts = AppointmentSlotFinder.GridStartsForDay(dstDate, 60, "Europe/London");

        Assert.Equal(24, starts.Count); // every nominal wall-clock hour still produces a candidate
        var distinct = starts.Distinct().ToList();
        Assert.Equal(23, distinct.Count); // exactly one collapses (01:00 and 02:00 -> the same instant)
    }
}
