using System.Reflection;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §11.7 G1 / ADR-159 -- U-A8: the staff-time rows equal HO-APT-001 §8.4's occupying predicate after EVERY method that changes status,
/// time, performer or assistant; and a surgery appointment refuses every other transition (<c>AppointmentManagedBySurgery</c>).</summary>
public sealed class AppointmentStaffTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddMinutes(30);
    private static readonly Guid Performer = Guid.NewGuid();

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static AppointmentAggregate Held()
    {
        var a = new AppointmentAggregate(Guid.NewGuid());
        a.PlaceHold(new SlotHoldDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(), Performer, Guid.NewGuid(), Start, End,
            BookingChannel.Online, null, Now.AddMinutes(10), TestUserContext.Create(), Guid.NewGuid()));
        return a;
    }

    private static AppointmentAggregate Booked()
    {
        var a = Held();
        a.Confirm(new AppointmentConfirmDto(Now, null, TestUserContext.Create(), Guid.NewGuid()));
        return a;
    }

    private static AppointmentAggregate CheckedIn()
    {
        var a = Booked();
        a.CheckIn(new AppointmentCheckInDto(Start, "UTC", 60, null, TestUserContext.Create(), Guid.NewGuid()));
        return a;
    }

    private static AppointmentAggregate Surgery(Guid? surgeryId = null, Guid? assistant = null)
    {
        var a = new AppointmentAggregate(Guid.NewGuid());
        a.BookForSurgery(new SurgeryAppointmentDto(
            surgeryId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new ServiceSnapshot("Spay", ServiceCategory.Spay, 60, new Money(5000m, "BDT")),
            Performer, assistant ?? Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddHours(1), TestUserContext.Create()));
        return a;
    }

    /// <summary>The predicate, written out here and not read from the aggregate.</summary>
    private static bool ShouldOccupy(AppointmentAggregate a) =>
        !a.IsWalkIn && (a.Status == AppointmentStatus.Held || a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation);

    private static void AssertRowsMatch(AppointmentAggregate a)
    {
        var expectedRoles = new List<(StaffTimeRole Role, Guid Staff)>();
        if (a.PerformerId is { } p && a.StartsOn is not null) expectedRoles.Add((StaffTimeRole.Performer, p));
        if (a.AssistantId is { } s && a.StartsOn is not null) expectedRoles.Add((StaffTimeRole.Assistant, s));

        Assert.Equal(expectedRoles.Count, a.StaffTime.Count);
        foreach (var (role, staff) in expectedRoles)
        {
            var row = Assert.Single(a.StaffTime, r => r.Role == role);
            Assert.Equal(staff, row.StaffAccountId);
            Assert.Equal(a.StartsOn, row.StartsOn);
            Assert.Equal(a.EndsOn, row.EndsOn);
            Assert.Equal(ShouldOccupy(a), row.Occupies);
        }
    }

    [Fact]
    public void U_A8_EveryStatusOrTimeChangingMethod_LeavesTheRowsEqualToTheOccupyingPredicate()
    {
        var held = Held();
        AssertRowsMatch(held); // PlaceHold: a hold occupies

        held.ReleaseHold(TestUserContext.Create(), Now);
        AssertRowsMatch(held); // ReleaseHold: no longer

        var expired = Held();
        expired.ExpireHold(expired.HoldExpiresOn!.Value.AddSeconds(1));
        AssertRowsMatch(expired); // ExpireHold

        var booked = Booked();
        AssertRowsMatch(booked); // Confirm

        booked.Reschedule(new AppointmentRescheduleDto(Start.AddDays(1), End.AddDays(1), Guid.NewGuid(), Guid.NewGuid(), Now, null, true, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(booked); // Reschedule: new time and performer follow

        booked.Cancel(new AppointmentCancelDto(Guid.NewGuid(), false, "No longer needed", Now, null, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(booked); // Cancel

        var direct = new AppointmentAggregate(Guid.NewGuid());
        direct.BookDirect(new DirectBookingDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(), Performer, Guid.NewGuid(), Start, End, null, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(direct); // BookDirect

        var checkedIn = CheckedIn();
        AssertRowsMatch(checkedIn); // CheckIn: still occupies

        checkedIn.Start(new AppointmentStartDto(null, null, Start, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(checkedIn); // Start: InConsultation occupies

        checkedIn.Complete(new AppointmentCompleteDto(End, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(checkedIn); // Complete: no longer

        var noShow = Booked();
        noShow.RecordNoShow(new AppointmentNoShowDto(Guid.NewGuid(), Start.AddHours(1), 15, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(noShow); // RecordNoShow

        var walkIn = new AppointmentAggregate(Guid.NewGuid());
        walkIn.RegisterWalkIn(new WalkInRegisterDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(), TriagePriority.Routine, null, Now, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(walkIn); // RegisterWalkIn: no performer, no time, so no row

        walkIn.Start(new AppointmentStartDto(Performer, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        AssertRowsMatch(walkIn); // a walk-in never occupies a slot
    }

    /// <summary>U-A8 (reflection) -- a NEW public method on the aggregate must be classified here: either it touches occupancy (and is exercised above) or it is listed as not.</summary>
    [Fact]
    public void U_A8_EveryPublicMutationMethod_IsClassified()
    {
        var occupancyChanging = new HashSet<string>
        {
            "PlaceHold", "Confirm", "ReleaseHold", "ExpireHold", "BookDirect", "Reschedule", "Cancel", "CheckIn", "RegisterWalkIn", "Start", "Complete",
            "RecordNoShow", "BookForSurgery", "MoveForSurgery", "CancelForSurgery", "CompleteSurgerySlot",
        };
        var notOccupancy = new HashSet<string> { "SetTriagePriority", "FlagForReschedule", "ClearRescheduleFlag", "ReassignOwner" };

        var methods = typeof(AppointmentAggregate).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().ToList();

        var unclassified = methods.Where(m => !occupancyChanging.Contains(m) && !notOccupancy.Contains(m)).ToList();
        Assert.True(unclassified.Count == 0, "Classify these AppointmentAggregate methods for SyncStaffTime (ADR-159): " + string.Join(", ", unclassified));
    }

    [Fact]
    public void BookForSurgery_HoldsTheSurgeonAndTheAssistant_ThroughTwoRows()
    {
        var assistant = Guid.NewGuid();
        var a = Surgery(assistant: assistant);

        Assert.Equal(BookingChannel.Surgery, a.Channel);
        Assert.Equal(AppointmentStatus.Booked, a.Status);
        Assert.Equal(2, a.StaffTime.Count);
        Assert.Contains(a.StaffTime, r => r.Role == StaffTimeRole.Performer && r.StaffAccountId == Performer && r.Occupies);
        Assert.Contains(a.StaffTime, r => r.Role == StaffTimeRole.Assistant && r.StaffAccountId == assistant && r.Occupies);
        AssertRowsMatch(a);
    }

    [Fact]
    public void MoveForSurgery_UpdatesTheRowsInPlace_ChangesTheStamp_AndClearsTheRescheduleFlag()
    {
        var surgeryId = Guid.NewGuid();
        var a = Surgery(surgeryId);
        a.FlagForReschedule(new AppointmentRescheduleFlagDto(RescheduleFlagReason.PerformerOnLeave, Guid.NewGuid(), TestUserContext.Create(), Guid.NewGuid()));
        var rowIds = a.StaffTime.Select(r => r.Id).ToHashSet();
        var oldStamp = a.ScheduleStamp;
        var newAssistant = Guid.NewGuid();

        a.MoveForSurgery(new SurgeryAppointmentMoveDto(surgeryId, Performer, newAssistant, a.RoomId!.Value, Start.AddDays(1), Start.AddDays(1).AddHours(1), TestUserContext.Create()));

        Assert.NotEqual(oldStamp, a.ScheduleStamp);
        Assert.False(a.NeedsRescheduling);
        Assert.Equal(rowIds, a.StaffTime.Select(r => r.Id).ToHashSet()); // updated in place, never re-inserted
        Assert.Equal(newAssistant, a.StaffTime.Single(r => r.Role == StaffTimeRole.Assistant).StaffAccountId);
        AssertRowsMatch(a);
    }

    [Fact]
    public void CancelForSurgery_AndCompleteSurgerySlot_FreeTheOccupancy()
    {
        var surgeryId = Guid.NewGuid();
        var cancelled = Surgery(surgeryId);
        cancelled.CancelForSurgery(new SurgeryAppointmentCancelDto(surgeryId, "Owner changed their mind", Now, TestUserContext.Create()));
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
        Assert.All(cancelled.StaffTime, r => Assert.False(r.Occupies));

        var completed = Surgery(surgeryId);
        completed.CompleteSurgerySlot(new SurgeryAppointmentCompleteDto(surgeryId, Start.AddMinutes(50), TestUserContext.Create()));
        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        Assert.All(completed.StaffTime, r => Assert.False(r.Occupies));
    }

    /// <summary>§8.4 -- the appointment cannot diverge from the surgery: every non-surgery transition is refused with AppointmentManagedBySurgery.</summary>
    [Fact]
    public void ASurgeryAppointment_RefusesEveryOtherTransition()
    {
        var a = Surgery();
        var user = TestUserContext.Create();
        var cid = Guid.NewGuid();

        var attempts = new Action[]
        {
            () => a.Reschedule(new AppointmentRescheduleDto(Start.AddDays(1), End.AddDays(1), Performer, Guid.NewGuid(), Now, null, true, user, cid)),
            () => a.Cancel(new AppointmentCancelDto(Guid.NewGuid(), false, "x", Now, null, user, cid)),
            () => a.CheckIn(new AppointmentCheckInDto(Start, "UTC", 60, null, user, cid)),
            () => a.SetTriagePriority(new AppointmentTriageDto(TriagePriority.Urgent, user, cid)),
            () => a.Start(new AppointmentStartDto(Performer, Guid.NewGuid(), Start, user, cid)),
            () => a.Complete(new AppointmentCompleteDto(End, user, cid)),
            () => a.RecordNoShow(new AppointmentNoShowDto(Guid.NewGuid(), Start.AddHours(2), 15, user, cid)),
        };

        foreach (var attempt in attempts)
        {
            var ex = Assert.Throws<DomainRuleViolationException>(attempt);
            Assert.Equal(BusinessViolationCodes.AppointmentManagedBySurgery, ex.ErrorCode);
        }

        Assert.Equal(AppointmentStatus.Booked, a.Status);
    }

    [Fact]
    public void TheSurgeryMethods_RefuseAnAppointmentOfAnotherSurgery_AndAnOrdinaryOne()
    {
        var a = Surgery();
        var ordinary = Booked();

        Assert.Throws<DomainRuleViolationException>(() => a.CancelForSurgery(new SurgeryAppointmentCancelDto(Guid.NewGuid(), "x", Now, TestUserContext.Create())));
        Assert.Throws<DomainRuleViolationException>(() => ordinary.CancelForSurgery(new SurgeryAppointmentCancelDto(Guid.NewGuid(), "x", Now, TestUserContext.Create())));
    }
}
