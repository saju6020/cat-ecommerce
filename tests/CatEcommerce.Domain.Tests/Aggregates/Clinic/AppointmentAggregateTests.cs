using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Clinic;

/// <summary>HO-APT-001 §8.3 -- U-A3..U-A7.</summary>
public sealed class AppointmentAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddMinutes(30);

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static AppointmentAggregate HeldAppointment(DateTimeOffset? holdExpiresOn = null)
    {
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(),
            Guid.NewGuid(), Guid.NewGuid(), Start, End, BookingChannel.Online, null,
            holdExpiresOn ?? Now.AddMinutes(10), TestUserContext.Create(), Guid.NewGuid()));
        return appointment;
    }

    private static AppointmentAggregate BookedAppointment()
    {
        var appointment = HeldAppointment(holdExpiresOn: Now.AddMinutes(30));
        appointment.Confirm(new AppointmentConfirmDto(Now, "Annual check", TestUserContext.Create(), Guid.NewGuid()));
        return appointment;
    }

    [Fact]
    public void Confirm_BeforeExpiry_TransitionsToBooked_AndAppendsHistory()
    {
        var appointment = HeldAppointment(holdExpiresOn: Now.AddMinutes(10));

        appointment.Confirm(new AppointmentConfirmDto(Now, null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Single(appointment.StatusHistory);
        Assert.Equal(AppointmentStatus.Held, appointment.StatusHistory[0].From);
        Assert.Equal(AppointmentStatus.Booked, appointment.StatusHistory[0].To);
    }

    /// <summary>U-A6 -- at HoldExpiresOn exactly is expired; one tick before is booked.</summary>
    [Fact]
    public void Confirm_AtExpiryExactly_ThrowsSlotHoldExpired()
    {
        var expiresOn = Now;
        var appointment = HeldAppointment(holdExpiresOn: expiresOn);

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            appointment.Confirm(new AppointmentConfirmDto(expiresOn, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.SlotHoldExpired, ex.ErrorCode);
    }

    [Fact]
    public void Confirm_OneTickBeforeExpiry_Succeeds()
    {
        var expiresOn = Now;
        var appointment = HeldAppointment(holdExpiresOn: expiresOn);

        appointment.Confirm(new AppointmentConfirmDto(expiresOn.AddTicks(-1), null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    /// <summary>Gate fix, HIGH #5 -- <c>ExpireHold</c> must enforce <c>now &gt;= HoldExpiresOn</c>
    /// itself; a live hold is refused (a no-op), not expired just because a caller asked.</summary>
    [Fact]
    public void ExpireHold_BeforeExpiry_IsRefused_AndStaysHeld()
    {
        var expiresOn = Now.AddMinutes(10);
        var appointment = HeldAppointment(holdExpiresOn: expiresOn);

        appointment.ExpireHold(expiresOn.AddTicks(-1));

        Assert.Equal(AppointmentStatus.Held, appointment.Status);
        Assert.Empty(appointment.StatusHistory);
    }

    [Fact]
    public void ExpireHold_AtOrAfterExpiry_TransitionsToHoldExpired()
    {
        var expiresOn = Now.AddMinutes(10);
        var appointment = HeldAppointment(holdExpiresOn: expiresOn);

        appointment.ExpireHold(expiresOn);

        Assert.Equal(AppointmentStatus.HoldExpired, appointment.Status);
    }

    /// <summary>Backlog row 6 -- a hold's expiry is a status change like any other and must be
    /// audited in StatusHistory (U-A3).</summary>
    [Fact]
    public void ExpireHold_AppendsAStatusHistoryRow()
    {
        var expiresOn = Now.AddMinutes(10);
        var appointment = HeldAppointment(holdExpiresOn: expiresOn);

        appointment.ExpireHold(expiresOn);

        var change = Assert.Single(appointment.StatusHistory);
        Assert.Equal(AppointmentStatus.Held, change.From);
        Assert.Equal(AppointmentStatus.HoldExpired, change.To);
        Assert.Equal(expiresOn, change.ChangedOn);
    }

    /// <summary>Backlog row 6 -- releasing a hold is audited too, against the releasing user.</summary>
    [Fact]
    public void ReleaseHold_AppendsAStatusHistoryRow_ByTheReleasingUser()
    {
        var appointment = HeldAppointment();
        var userContext = TestUserContext.Create();

        appointment.ReleaseHold(userContext, Now);

        Assert.Equal(AppointmentStatus.HoldReleased, appointment.Status);
        var change = Assert.Single(appointment.StatusHistory);
        Assert.Equal(AppointmentStatus.Held, change.From);
        Assert.Equal(AppointmentStatus.HoldReleased, change.To);
        Assert.Equal(userContext.UserId, change.ChangedBy);
    }

    /// <summary>Backlog row 5 -- Clinical/Forbidden history (C-ALL-4) is never trimmed, even at
    /// the old 20-entry cap.</summary>
    [Fact]
    public void Transition_NeverDeletesExistingStatusHistory()
    {
        var appointment = BookedAppointment();
        var field = typeof(AppointmentAggregate).GetField("_statusHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var history = (List<CatEcommerce.Domain.Entities.AppointmentStatusChange>)field.GetValue(appointment)!;
        while (history.Count < AppointmentRules.MaxStatusHistoryEntries)
            history.Add(new CatEcommerce.Domain.Entities.AppointmentStatusChange(Guid.NewGuid(), AppointmentStatus.Booked, AppointmentStatus.Booked, Now, Guid.NewGuid()));
        var firstId = history[0].Id;
        var countBefore = history.Count;

        appointment.Cancel(new AppointmentCancelDto(Guid.NewGuid(), false, "reason", Now, null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(countBefore + 1, appointment.StatusHistory.Count);
        Assert.Equal(firstId, appointment.StatusHistory[0].Id);
    }

    /// <summary>U-A4 -- grace period boundary.</summary>
    [Fact]
    public void RecordNoShow_BeforeGracePeriod_ThrowsNoShowTooEarly()
    {
        var appointment = BookedAppointment();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            appointment.RecordNoShow(new AppointmentNoShowDto(Guid.NewGuid(), Start.AddMinutes(14), 15, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.NoShowTooEarly, ex.ErrorCode);
    }

    [Fact]
    public void RecordNoShow_AtGracePeriod_Succeeds()
    {
        var appointment = BookedAppointment();

        appointment.RecordNoShow(new AppointmentNoShowDto(Guid.NewGuid(), Start.AddMinutes(15), 15, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }

    /// <summary>U-A5 -- the day before is refused; the same local day (or just before, within the
    /// early window) is allowed.</summary>
    [Fact]
    public void CheckIn_DayBeforeStart_ThrowsCheckInNotToday()
    {
        var appointment = BookedAppointment();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            appointment.CheckIn(new AppointmentCheckInDto(Start.AddDays(-1), "Asia/Dhaka", 60, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.CheckInNotToday, ex.ErrorCode);
    }

    [Fact]
    public void CheckIn_SameLocalDay_Succeeds()
    {
        var appointment = BookedAppointment();

        appointment.CheckIn(new AppointmentCheckInDto(Start.AddHours(-1), "Asia/Dhaka", 60, null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
    }

    /// <summary>APT backlog row 47 -- transitions at the same instant still carry a strictly
    /// increasing sequence, the read side's tie-breaker.</summary>
    [Fact]
    public void Transitions_AtTheSameInstant_GetStrictlyIncreasingSequence()
    {
        var appointment = BookedAppointment();
        var at = Start.AddHours(-1);

        appointment.CheckIn(new AppointmentCheckInDto(at, "Asia/Dhaka", 60, null, TestUserContext.Create(), Guid.NewGuid()));
        appointment.Start(new AppointmentStartDto(null, null, at, TestUserContext.Create(), Guid.NewGuid()));

        var sequences = appointment.StatusHistory.Select(h => h.Sequence).ToList();
        Assert.Equal(sequences.Count, sequences.Distinct().Count());
        Assert.Equal(sequences.OrderBy(x => x), sequences);
        Assert.All(sequences, x => Assert.True(x > 0));
    }

    /// <summary>U-A7 -- reschedule changes the stamp; a room-only move does not.</summary>
    [Fact]
    public void Reschedule_ChangingStart_ChangesScheduleStamp_AndIncrementsCount()
    {
        var appointment = BookedAppointment();
        var originalStamp = appointment.ScheduleStamp;
        var newStart = Start.AddHours(1);

        appointment.Reschedule(new AppointmentRescheduleDto(newStart, newStart.AddMinutes(30), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null, true, TestUserContext.Create(), Guid.NewGuid()));

        Assert.NotEqual(originalStamp, appointment.ScheduleStamp);
        Assert.Equal(1, appointment.RescheduleCount);
    }

    [Fact]
    public void Reschedule_RoomOnly_SameStart_DoesNotChangeScheduleStamp()
    {
        var appointment = BookedAppointment();
        var originalStamp = appointment.ScheduleStamp;
        var newRoomId = Guid.NewGuid();

        appointment.Reschedule(new AppointmentRescheduleDto(appointment.StartsOn!.Value, appointment.EndsOn!.Value, appointment.PerformerId!.Value, newRoomId, DateTimeOffset.UtcNow, null, true, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(originalStamp, appointment.ScheduleStamp);
        Assert.Equal(0, appointment.RescheduleCount);
        Assert.Equal(newRoomId, appointment.RoomId);
    }

    [Fact]
    public void Reschedule_CustomerPastCutoff_ThrowsChangeCutoffPassed()
    {
        var appointment = BookedAppointment();
        var now = Start.AddHours(-3); // cutoff is 4 hours

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            appointment.Reschedule(new AppointmentRescheduleDto(Start.AddHours(2), Start.AddHours(2.5), Guid.NewGuid(), Guid.NewGuid(), now, 4, false, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.ChangeCutoffPassed, ex.ErrorCode);
    }

    [Fact]
    public void Cancel_StaffAnyTime_Succeeds_NoCutoffCheck()
    {
        var appointment = BookedAppointment();

        appointment.Cancel(new AppointmentCancelDto(Guid.NewGuid(), false, "No longer needed", Start.AddMinutes(-5), null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Cancel_CustomerPastCutoff_ThrowsChangeCutoffPassed()
    {
        var appointment = BookedAppointment();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            appointment.Cancel(new AppointmentCancelDto(Guid.NewGuid(), true, null, Start.AddHours(-1), 4, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.ChangeCutoffPassed, ex.ErrorCode);
    }
}
