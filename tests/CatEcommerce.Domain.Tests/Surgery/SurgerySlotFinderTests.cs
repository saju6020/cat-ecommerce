using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §8.5 -- U-B1..U-B4: every clash is named, in the fixed order; the starts of a free day.</summary>
public sealed class SurgerySlotFinderTests
{
    private const string Zone = "Asia/Dhaka";
    private static readonly DateOnly Day = new(2026, 10, 12);
    private static readonly DateTimeOffset Midnight = new(2026, 10, 11, 18, 0, 0, TimeSpan.Zero); // 00:00 Dhaka on 12 Oct
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) => Midnight.AddHours(hour).AddMinutes(minute);

    private static SlotPerformerInput Staff(string name, string[] roles, int fromHour = 8, int toHour = 18) =>
        new(Guid.NewGuid(), name, roles, [new SlotAvailabilityWindow(At(fromHour), At(toHour), false)]);

    private static SlotRoomInput Theatre(bool active = true, RoomKind? kind = null) => new(Guid.NewGuid(), "Theatre 1", kind ?? RoomKind.Theatre, active);

    private static SurgerySlotRequest Request(
        SlotPerformerInput? surgeon = null, SlotPerformerInput? assistant = null, SlotRoomInput? theatre = null, string[]? qualified = null,
        IReadOnlyList<SurgeryBusyInterval>? surgeonBusy = null, IReadOnlyList<SurgeryBusyInterval>? assistantBusy = null,
        IReadOnlyList<SurgeryBusyInterval>? theatreBusy = null, IReadOnlyList<SurgeryBusyInterval>? catBusy = null, int duration = 60) =>
        new(new SurgeryProcedureInfo(duration, qualified ?? [StaffRoles.Vet]), Zone,
            surgeon ?? Staff("Dr Rahman", [StaffRoles.Vet]), assistant ?? Staff("Nurse Akter", [StaffRoles.Nurse]), theatre ?? Theatre(),
            surgeonBusy ?? [], assistantBusy ?? [], theatreBusy ?? [], catBusy ?? [], 15, Now);

    private static string[] Codes(IReadOnlyList<SurgeryClash> clashes) => clashes.Select(c => c.Code).ToArray();

    /// <summary>U-B4 -- a 60-minute procedure on a 15-minute grid, everything free 09:00-12:00: starts 09:00 to 11:00.</summary>
    [Fact]
    public void U_B4_AllFree_09To12_Offers09To11()
    {
        var request = Request(Staff("Dr Rahman", [StaffRoles.Vet], 9, 12), Staff("Nurse Akter", [StaffRoles.Nurse], 9, 12));

        var starts = SurgerySlotFinder.FindStarts(request, Day);

        Assert.Equal(9, starts.Count);
        Assert.Equal(At(9), starts[0].StartsOn);
        Assert.Equal(At(10), starts[0].EndsOn);
        Assert.Equal(At(11), starts[^1].StartsOn);
        Assert.Equal(At(12), starts[^1].EndsOn);
    }

    [Fact]
    public void FindStarts_NeedsAllThree_SurgeonAssistantAndTheatre()
    {
        Assert.Empty(SurgerySlotFinder.FindStarts(Request() with { Surgeon = null }, Day));
        Assert.Empty(SurgerySlotFinder.FindStarts(Request() with { Assistant = null }, Day));
        Assert.Empty(SurgerySlotFinder.FindStarts(Request() with { Theatre = null }, Day));
    }

    /// <summary>U-B1 -- the surgeon busy, the assistant on leave and the theatre busy: three clashes in the fixed order, each with its property and a message naming the time.</summary>
    [Fact]
    public void U_B1_Explain_NamesEveryClash_InTheFixedOrder_WithTheirProperties()
    {
        var assistant = new SlotPerformerInput(Guid.NewGuid(), "Nurse Akter", [StaffRoles.Nurse], []); // on leave: no availability at all
        var request = Request(
            assistant: assistant,
            surgeonBusy: [new SurgeryBusyInterval(At(10), At(11), SurgeryBusyKind.AsSurgeon)],
            theatreBusy: [new SurgeryBusyInterval(At(9, 30), At(10, 30), SurgeryBusyKind.Booking)]);

        var clashes = SurgerySlotFinder.Explain(request, At(10));

        Assert.Equal([BusinessViolationCodes.AssistantNotAvailable, BusinessViolationCodes.TheatreBusy, BusinessViolationCodes.SurgeonBusy], Codes(clashes));
        Assert.Equal(["assistantId", "theatreId", "surgeonId"], clashes.Select(c => c.Property).ToArray());
        Assert.Contains("09:30–10:30", clashes[1].Message);
        Assert.Contains("10:00–11:00", clashes[2].Message);
        Assert.Contains("Dr Rahman", clashes[2].Message);
    }

    /// <summary>U-B2 -- a receptionist assistant, an assistant who is the surgeon, a nurse surgeon for a vet-only service.</summary>
    [Fact]
    public void U_B2_Explain_RefusesAnUnqualifiedAssistant_TheSamePerson_AndAnUnqualifiedSurgeon()
    {
        var receptionist = Staff("Reception Rina", [StaffRoles.Reception]);
        Assert.Contains(BusinessViolationCodes.AssistantNotQualified, Codes(SurgerySlotFinder.Explain(Request(assistant: receptionist), At(10))));

        var vet = Staff("Dr Rahman", [StaffRoles.Vet]);
        var samePerson = SurgerySlotFinder.Explain(Request(surgeon: vet, assistant: vet), At(10));
        Assert.Contains(BusinessViolationCodes.AssistantSameAsSurgeon, Codes(samePerson));

        var nurseSurgeon = Staff("Nurse Akter", [StaffRoles.Nurse]);
        Assert.Contains(BusinessViolationCodes.SurgeonNotQualified, Codes(SurgerySlotFinder.Explain(Request(surgeon: nurseSurgeon), At(10))));
    }

    /// <summary>U-B3 -- the assistant busy as the PERFORMER of a vaccination; the surgeon busy as the ASSISTANT of another surgery.</summary>
    [Fact]
    public void U_B3_Explain_AStaffMemberIsBusy_AsPerformerOrAsAssistant()
    {
        var request = Request(
            assistantBusy: [new SurgeryBusyInterval(At(10), At(10, 15), SurgeryBusyKind.AsPerformer)],
            surgeonBusy: [new SurgeryBusyInterval(At(10, 30), At(11, 30), SurgeryBusyKind.AsAssistant)]);

        var atTen = SurgerySlotFinder.Explain(request, At(10));
        Assert.Equal([BusinessViolationCodes.SurgeonBusy, BusinessViolationCodes.AssistantBusy], Codes(atTen));
        Assert.Contains("is assisting another surgery", atTen[0].Message);
        Assert.Contains("10:30–11:30", atTen[0].Message);
        Assert.Contains("already has an appointment", atTen[1].Message);
    }

    [Fact]
    public void Explain_AStartInThePast_OffTheGrid_OrWithTheCatBooked_IsNamed()
    {
        var inPast = Request() with { EarliestStart = At(12) };
        Assert.Contains(BusinessViolationCodes.SlotInPast, Codes(SurgerySlotFinder.Explain(inPast, At(10))));

        Assert.Contains(BusinessViolationCodes.SlotNotOnGrid, Codes(SurgerySlotFinder.Explain(Request(), At(10, 7))));

        var catBusy = Request(catBusy: [new SurgeryBusyInterval(At(9, 30), At(10, 30), SurgeryBusyKind.Booking)]);
        var clash = Assert.Single(SurgerySlotFinder.Explain(catBusy, At(10)));
        Assert.Equal(BusinessViolationCodes.CatAlreadyBooked, clash.Code);
        Assert.Equal("catId", clash.Property);
    }

    [Fact]
    public void Explain_ATheatreThatIsInactiveOrNotATheatre_IsNotAvailable()
    {
        Assert.Contains(BusinessViolationCodes.TheatreNotAvailable, Codes(SurgerySlotFinder.Explain(Request(theatre: Theatre(active: false)), At(10))));
        Assert.Contains(BusinessViolationCodes.TheatreNotAvailable, Codes(SurgerySlotFinder.Explain(Request(theatre: Theatre(kind: RoomKind.Consultation)), At(10))));
    }

    [Fact]
    public void Explain_ATimeOutsideRosteredAvailability_IsNotAvailable_AndTheEdgesAreInclusive()
    {
        var surgeon = Staff("Dr Rahman", [StaffRoles.Vet], 9, 12);
        var request = Request(surgeon: surgeon);

        Assert.Contains(BusinessViolationCodes.SurgeonNotAvailable, Codes(SurgerySlotFinder.Explain(request, At(8, 30))));
        Assert.Contains(BusinessViolationCodes.SurgeonNotAvailable, Codes(SurgerySlotFinder.Explain(request, At(11, 15)))); // ends 12:15
        Assert.DoesNotContain(BusinessViolationCodes.SurgeonNotAvailable, Codes(SurgerySlotFinder.Explain(request, At(11)))); // ends exactly 12:00
        Assert.DoesNotContain(BusinessViolationCodes.SurgeonNotAvailable, Codes(SurgerySlotFinder.Explain(request, At(9))));
    }

    [Fact]
    public void ABusyIntervalThatJustAbuts_DoesNotClash()
    {
        var request = Request(theatreBusy: [new SurgeryBusyInterval(At(9), At(10), SurgeryBusyKind.Booking)]);

        Assert.Empty(SurgerySlotFinder.Explain(request, At(10)));
        Assert.NotEmpty(SurgerySlotFinder.Explain(request, At(9, 45)));
    }

    [Fact]
    public void AShiftOutsideOpeningHours_IsOfferedAndFlagged()
    {
        var surgeon = new SlotPerformerInput(Guid.NewGuid(), "Dr Rahman", [StaffRoles.Vet], [new SlotAvailabilityWindow(At(9), At(12), true)]);

        var starts = SurgerySlotFinder.FindStarts(Request(surgeon: surgeon, assistant: Staff("Nurse Akter", [StaffRoles.Nurse], 9, 12)), Day);

        Assert.NotEmpty(starts);
        Assert.All(starts, s => Assert.True(s.OutsideOpeningHours));
    }
}
