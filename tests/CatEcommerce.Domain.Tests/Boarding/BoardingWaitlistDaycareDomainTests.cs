using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Domain.Tests.Boarding;

/// <summary>HO-BRD-001 Phase H -- U-H1 .. U-H9: the waitlist entry, the daycare visit and daycare pricing.</summary>
public sealed class BoardingWaitlistDaycareDomainTests
{
    private static readonly UserContext Customer = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static BoardingWaitlistEntryAggregate Waiting(DateOnly? arrival = null)
    {
        var entry = new BoardingWaitlistEntryAggregate(Guid.NewGuid());
        entry.Join(Guid.NewGuid(), Customer.UserId, arrival ?? Today.AddDays(5), (arrival ?? Today.AddDays(5)).AddDays(3), 1, null, Today, Now, Customer);
        return entry;
    }

    private static DaycareRateCardAggregate Card()
    {
        var card = new DaycareRateCardAggregate(Guid.NewGuid());
        card.Create(Guid.NewGuid(), 200m, 1000m, Guid.NewGuid(), "BDT", Now.AddDays(1), Now, Customer);
        return card;
    }

    private static DaycareVisitAggregate Booked(DaycareBasis? basis = null, int? hours = 3)
    {
        var visit = new DaycareVisitAggregate(Guid.NewGuid());
        visit.Book(Guid.NewGuid(), Customer.UserId, Guid.NewGuid(), "Mitu", Today, 1, basis ?? DaycareBasis.Hourly, hours, Card(), Today, Now, Customer);
        return visit;
    }

    [Fact]
    public void U_H1_AnEntryNeedsAFutureArrivalALaterDepartureAndOneToSixCats()
    {
        var entry = new BoardingWaitlistEntryAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => entry.Join(Guid.NewGuid(), Customer.UserId, Today.AddDays(-1), Today.AddDays(2), 1, null, Today, Now, Customer));
        Assert.Throws<DomainRuleViolationException>(() => entry.Join(Guid.NewGuid(), Customer.UserId, Today.AddDays(3), Today.AddDays(3), 1, null, Today, Now, Customer));
        Assert.Throws<DomainRuleViolationException>(() => entry.Join(Guid.NewGuid(), Customer.UserId, Today.AddDays(3), Today.AddDays(5), 7, null, Today, Now, Customer));
    }

    [Fact]
    public void U_H2_AWaitingEntryIsNotifiedOnce_AndWithdrawingEndsItFromEitherLiveState()
    {
        var entry = Waiting();

        Assert.True(entry.MarkNotified(Now));
        Assert.False(entry.MarkNotified(Now.AddMinutes(1)));
        Assert.Equal(BoardingWaitlistStatus.Notified, entry.Status);

        entry.Withdraw(Now.AddHours(1));
        entry.Withdraw(Now.AddHours(2));
        Assert.Equal(BoardingWaitlistStatus.Withdrawn, entry.Status);
        Assert.Equal(Now.AddHours(1), entry.EndedOn);
        Assert.False(entry.IsLive);
    }

    [Fact]
    public void U_H3_OnlyAWaitingEntryWhoseArrivalHasPassedIsClosed()
    {
        var entry = Waiting(Today.AddDays(1));

        Assert.False(entry.CloseIfStale(Today, Now));
        Assert.False(entry.CloseIfStale(Today.AddDays(1), Now));
        Assert.True(entry.CloseIfStale(Today.AddDays(2), Now));
        Assert.Equal(BoardingWaitlistStatus.Closed, entry.Status);

        var notified = Waiting(Today.AddDays(1));
        notified.MarkNotified(Now);
        Assert.False(notified.CloseIfStale(Today.AddDays(9), Now));
    }

    [Theory]
    [InlineData("Hourly", 0.5, 200)]    // at least one hour
    [InlineData("Hourly", 2.5, 600)]    // rounded up to 3 hours
    [InlineData("Hourly", 4, 800)]
    [InlineData("Hourly", 9, 1000)]     // capped at the day rate
    [InlineData("Day", 1, 1000)]
    [InlineData("Day", 11, 1000)]
    public void U_H4_DaycareCharge_IsHoursRoundedUpTimesTheHourlyRate_CappedAtTheDayRate(string basis, double hours, int expected)
    {
        var charge = DaycarePricing.Charge(basis == "Day" ? DaycareBasis.Day : DaycareBasis.Hourly, 200m, 1000m, TimeSpan.FromHours(hours));

        Assert.Equal(expected, charge);
    }

    [Fact]
    public void U_H5_ARateCardCannotBeBackdatedOrNegative()
    {
        var card = new DaycareRateCardAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => card.Create(Guid.NewGuid(), 200m, 1000m, Guid.NewGuid(), "BDT", Now.AddDays(-1), Now, Customer));
        Assert.Throws<DomainRuleViolationException>(() => card.Create(Guid.NewGuid(), -1m, 1000m, Guid.NewGuid(), "BDT", Now, Now, Customer));
        Assert.Throws<DomainRuleViolationException>(() => card.Create(Guid.NewGuid(), 200.123m, 1000m, Guid.NewGuid(), "BDT", Now, Now, Customer));
    }

    [Fact]
    public void U_H6_AVisitSnapshotsThePrice_AndNeedsExpectedHoursWhenHourly()
    {
        var visit = Booked(DaycareBasis.Hourly, 3);

        Assert.Equal(200m, visit.HourlyRate);
        Assert.Equal(1000m, visit.DayRate);
        Assert.Equal(DaycareVisitStatus.Booked, visit.Status);
        Assert.Throws<DomainRuleViolationException>(() =>
            new DaycareVisitAggregate(Guid.NewGuid()).Book(Guid.NewGuid(), Customer.UserId, Guid.NewGuid(), "Mitu", Today, 1, DaycareBasis.Hourly, null, Card(), Today, Now, Customer));
        Assert.Throws<DomainRuleViolationException>(() =>
            new DaycareVisitAggregate(Guid.NewGuid()).Book(Guid.NewGuid(), Customer.UserId, Guid.NewGuid(), "Mitu", Today.AddDays(-1), 1, DaycareBasis.Day, null, Card(), Today, Now, Customer));
    }

    [Fact]
    public void U_H7_AVisitIsCheckedInOnItsDayOnly_ThenCheckedOutWithItsCharge()
    {
        var visit = Booked(DaycareBasis.Hourly, 3);

        Assert.Throws<DomainRuleViolationException>(() => visit.CheckIn(Today.AddDays(1), Now));
        visit.CheckIn(Today, Now);
        visit.CheckOut(Now.AddHours(2).AddMinutes(10), Customer, Guid.NewGuid());

        Assert.Equal(DaycareVisitStatus.CheckedOut, visit.Status);
        Assert.Equal(600m, visit.ChargeAmount);
        Assert.Throws<DomainRuleViolationException>(() => visit.CheckOut(Now.AddHours(3), Customer, Guid.NewGuid()));
    }

    [Fact]
    public void U_H8_OnlyABookedVisitIsCancelledOrNoShown_AndNoShowNeedsTheDayToHavePassed()
    {
        var cancelled = Booked();
        cancelled.Cancel();
        Assert.Equal(DaycareVisitStatus.Cancelled, cancelled.Status);
        Assert.Throws<DomainRuleViolationException>(() => cancelled.Cancel());

        var visit = Booked();
        Assert.Throws<DomainRuleViolationException>(() => visit.RecordNoShow(Today));
        visit.RecordNoShow(Today.AddDays(1));
        Assert.Equal(DaycareVisitStatus.NoShow, visit.Status);
    }

    [Fact]
    public void U_H9_AMergedCustomersEntriesAndVisitsFollowTheSurvivor_Idempotently()
    {
        var survivor = Guid.NewGuid();
        var entry = Waiting();
        var visit = Booked();

        entry.ReassignOwner(survivor);
        visit.ReassignOwner(survivor);
        var version = entry.Version;
        entry.ReassignOwner(survivor);

        Assert.Equal(survivor, entry.CustomerId);
        Assert.Equal(survivor, visit.CustomerId);
        Assert.Equal(version, entry.Version);
    }
}
