using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Notifications.Guards;

/// <summary>
/// HO-EMR-001 §15 U-G2 -- <c>VisitFollowUpGuard</c> (ADR-131): valid only while the visit's follow-up still carries the request's
/// stamp, the cat is alive and owned by the recipient, and the cat has no <c>Booked</c> appointment on or after today in the
/// visit's branch zone. Anything unreadable is "not valid".
/// </summary>
public sealed class VisitFollowUpGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero); // 15:00 in Dhaka
    private static UserContext Ctx => new(Guid.NewGuid(), Guid.NewGuid());

    private sealed class MemoryRepository<T> : IAggregateRootRepository<T> where T : AggregateRoot
    {
        private readonly Dictionary<Guid, T> _store = [];
        public void Seed(T aggregate) => _store[aggregate.Id] = aggregate;
        public Task<T> GetByIdAsync(Guid id) => Task.FromResult(_store.TryGetValue(id, out var value) ? value : null!);
        public Task<T> GetByFilterAsync(Expression<Func<T, bool>> dataFilters) => Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(dataFilters)!);
        public Task<bool> ExistAsync(Expression<Func<T, bool>> dataFilters) => Task.FromResult(_store.Values.AsQueryable().Any(dataFilters));
        public Task SaveAsync(T aggregate, EventPublishOption eventPublishOption) { _store[aggregate.Id] = aggregate; return Task.CompletedTask; }
        public Task UpdateAsync(T aggregate, EventPublishOption eventPublishOption) => SaveAsync(aggregate, eventPublishOption);
        public Task UpdateAsync(T aggregate, int expectedVersion, EventPublishOption eventPublishOption) => SaveAsync(aggregate, eventPublishOption);
    }

    private sealed class Rig
    {
        public MemoryRepository<VisitAggregate> Visits { get; } = new();
        public MemoryRepository<CatAggregate> Cats { get; } = new();
        public MemoryRepository<AppointmentAggregate> Appointments { get; } = new();
        public MemoryRepository<BranchAggregate> Branches { get; } = new();
        public BranchAggregate Branch { get; } = new(Guid.NewGuid());
        public CatAggregate Cat { get; } = new(Guid.NewGuid());
        public VisitAggregate Visit { get; } = new(Guid.NewGuid());
        public Guid OwnerId { get; } = Guid.NewGuid();
        public FakeTimeProvider Clock { get; } = new(Now);

        public Rig(bool withFollowUp = true)
        {
            Branch.Create(new BranchCreateDto("GRD" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(), "Test branch", "Asia/Dhaka", null, null, null, Ctx, Guid.NewGuid()));
            Cat.Register(new CatRegistrationDto(OwnerId, "Mitu", "Cat", CatSex.Female, new DateOnly(2024, 1, 1), DateOfBirthPrecision.Exact, null, null, new DateOnly(2026, 10, 7), Ctx, Guid.NewGuid()));
            Visit.Open(new VisitOpeningDto(Branch.Id, Guid.NewGuid(), Cat.Id, AppointmentStatus.CheckedIn, CatLifeStatus.Alive, Guid.NewGuid(), "Consultation", ServiceCategory.Consultation, Now, Ctx, Guid.NewGuid()));
            if (withFollowUp) SetFollowUp();

            Visits.Seed(Visit);
            Cats.Seed(Cat);
            Branches.Seed(Branch);
        }

        public string SetFollowUp()
        {
            Visit.SetFollowUp(new FollowUpDto(FollowUpKind.Reminder, new DateOnly(2026, 10, 20), null, false, new DateOnly(2026, 10, 7), Guid.NewGuid(), Now, Ctx, Guid.NewGuid()));
            return Visit.FollowUp!.Stamp;
        }

        public AppointmentAggregate BookAppointment(DateTimeOffset startsOn)
        {
            var appointment = new AppointmentAggregate(Guid.NewGuid());
            appointment.PlaceHold(new SlotHoldDto(
                Branch.Id, OwnerId, Cat.Id, Guid.NewGuid(), new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
                Guid.NewGuid(), Guid.NewGuid(), startsOn, startsOn.AddMinutes(30), BookingChannel.Online, null, Now.AddMinutes(10), Ctx, Guid.NewGuid()));
            appointment.Confirm(new AppointmentConfirmDto(Now, null, Ctx, Guid.NewGuid()));
            Appointments.Seed(appointment);
            return appointment;
        }

        public VisitFollowUpGuard Guard() => new(Visits, Cats, Appointments, Branches, Clock);

        public Task<bool> IsValidAsync(string? stamp, Guid? recipient = null, Guid? subjectId = null) =>
            Guard().IsStillValidAsync(recipient ?? OwnerId, subjectId ?? Visit.Id, null, null, stamp);
    }

    [Fact]
    public async Task TheSameStamp_ForTheOwner_WithNoBookedAppointment_IsValid()
    {
        var rig = new Rig();
        Assert.True(await rig.IsValidAsync(rig.Visit.FollowUp!.Stamp));
    }

    [Fact]
    public async Task AChangedFollowUp_InvalidatesTheOldReminder()
    {
        var rig = new Rig();
        var old = rig.Visit.FollowUp!.Stamp;

        rig.SetFollowUp(); // a new stamp every set

        Assert.False(await rig.IsValidAsync(old));
        Assert.True(await rig.IsValidAsync(rig.Visit.FollowUp!.Stamp));
    }

    [Fact]
    public async Task AClearedFollowUp_IsNeverValid()
    {
        var rig = new Rig();
        var stamp = rig.Visit.FollowUp!.Stamp;

        rig.Visit.ClearFollowUp(new FollowUpClearDto(Now, Ctx, Guid.NewGuid()));

        Assert.False(await rig.IsValidAsync(stamp));
    }

    [Fact]
    public async Task ABookedFutureAppointment_ForTheCat_StandsTheReminderDown()
    {
        var rig = new Rig();
        var stamp = rig.Visit.FollowUp!.Stamp;

        rig.BookAppointment(Now.AddDays(10));

        Assert.False(await rig.IsValidAsync(stamp));
    }

    [Fact]
    public async Task AnAppointmentEarlierToday_StillCountsAsBooked_ButOneFromYesterdayDoesNot()
    {
        var yesterday = new Rig();
        yesterday.BookAppointment(Now.AddDays(-1));
        Assert.True(await yesterday.IsValidAsync(yesterday.Visit.FollowUp!.Stamp));

        // 09:30 Dhaka today (03:30 UTC) is "on or after today" in the branch zone even though it is before now.
        var today = new Rig();
        today.BookAppointment(new DateTimeOffset(2026, 10, 7, 3, 30, 0, TimeSpan.Zero));
        Assert.False(await today.IsValidAsync(today.Visit.FollowUp!.Stamp));
    }

    [Fact]
    public async Task ADeceasedCat_OrAnotherRecipient_OrNoStamp_OrAnUnknownVisit_IsNotValid()
    {
        var rig = new Rig();
        var stamp = rig.Visit.FollowUp!.Stamp;

        Assert.False(await rig.IsValidAsync(stamp, recipient: Guid.NewGuid()));
        Assert.False(await rig.IsValidAsync(null));
        Assert.False(await rig.IsValidAsync(stamp, subjectId: Guid.NewGuid()));

        rig.Cat.MarkDeceased(new CatDeceasedDto(new DateOnly(2026, 10, 6), null, new DateOnly(2026, 10, 7), Ctx, Guid.NewGuid()));
        Assert.False(await rig.IsValidAsync(stamp));
    }

    [Fact]
    public async Task ACatThatChangedOwner_StopsTheReminderForTheOldOwner()
    {
        var rig = new Rig();
        var stamp = rig.Visit.FollowUp!.Stamp;
        var newOwner = Guid.NewGuid();

        rig.Cat.TransferOwnership(new CatOwnershipTransferDto(newOwner, "sold", Ctx, Guid.NewGuid()));

        Assert.False(await rig.IsValidAsync(stamp, recipient: rig.OwnerId));
        Assert.True(await rig.IsValidAsync(stamp, recipient: newOwner));
    }

    [Fact]
    public void TheGuardIsForTheVisitFollowUpSubjectKind()
    {
        Assert.Equal(NotificationSubjectKind.VisitFollowUp, new Rig().Guard().Kind);
    }
}
