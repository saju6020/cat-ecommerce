using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Notifications.Guards;

/// <summary>
/// HO-APT-001 §16 U-D2 -- ACCEPTANCE gate fix (2026-09-28, A13). Valid only while the appointment is
/// <see cref="AppointmentStatus.Booked"/> and its own current <c>ScheduleStamp</c> matches the
/// reminder request's captured <c>SubjectStamp</c> (§8.9, ADR-074) -- a reschedule or cancellation
/// changes or leaves the stamp behind, so a stale reminder must fail. Exercised directly against a
/// real <see cref="AppointmentAggregate"/> in memory, matching <c>CatSubjectGuardTests</c>'s own
/// pattern for the sibling care-reminder guards.
/// </summary>
public sealed class AppointmentReminderGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 6, 0, 0, TimeSpan.Zero);
    private static UserContext Ctx => new(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>Hand-written in-memory test double -- matches
    /// CatEcommerce.Application.Tests.TestSupport.FakeAggregateRootRepository's own shape, kept
    /// local here since Infrastructure.Tests does not reference the Application test project.</summary>
    private sealed class FakeAppointmentRepository : IAggregateRootRepository<AppointmentAggregate>
    {
        private readonly Dictionary<Guid, AppointmentAggregate> _store = [];

        public void Seed(AppointmentAggregate appointment) => _store[appointment.Id] = appointment;

        public Task<AppointmentAggregate> GetByIdAsync(Guid id) =>
            Task.FromResult(_store.TryGetValue(id, out var value) ? value : null!);

        public Task<AppointmentAggregate> GetByFilterAsync(Expression<Func<AppointmentAggregate, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(dataFilters)!);

        public Task<bool> ExistAsync(Expression<Func<AppointmentAggregate, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().Any(dataFilters));

        public Task SaveAsync(AppointmentAggregate aggregate, EventPublishOption eventPublishOption)
        {
            _store[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AppointmentAggregate aggregate, EventPublishOption eventPublishOption)
        {
            _store[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AppointmentAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption) =>
            UpdateAsync(aggregate, eventPublishOption);
    }

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static AppointmentAggregate BookedAppointment(Guid customerId)
    {
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), customerId, Guid.NewGuid(), Guid.NewGuid(), Snapshot(),
            Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(30), BookingChannel.Online, null,
            Now.AddMinutes(10), Ctx, Guid.NewGuid()));
        appointment.Confirm(new AppointmentConfirmDto(Now, "Annual check", Ctx, Guid.NewGuid()));
        return appointment;
    }

    [Fact]
    public async Task Booked_SameStamp_IsValid()
    {
        var customerId = Guid.NewGuid();
        var appointment = BookedAppointment(customerId);
        var repository = new FakeAppointmentRepository();
        repository.Seed(appointment);
        var guard = new AppointmentReminderGuard(repository);

        var valid = await guard.IsStillValidAsync(customerId, appointment.Id, null, null, appointment.ScheduleStamp);

        Assert.True(valid);
    }

    [Fact]
    public async Task Rescheduled_StampChanged_IsNotValidForTheOldStamp()
    {
        var customerId = Guid.NewGuid();
        var appointment = BookedAppointment(customerId);
        var originalStamp = appointment.ScheduleStamp;
        var newStart = Start.AddDays(1);
        appointment.Reschedule(new AppointmentRescheduleDto(
            newStart, newStart.AddMinutes(30), Guid.NewGuid(), Guid.NewGuid(), Now, 4, false, Ctx, Guid.NewGuid()));
        var repository = new FakeAppointmentRepository();
        repository.Seed(appointment);
        var guard = new AppointmentReminderGuard(repository);

        var validForOldStamp = await guard.IsStillValidAsync(customerId, appointment.Id, null, null, originalStamp);
        var validForNewStamp = await guard.IsStillValidAsync(customerId, appointment.Id, null, null, appointment.ScheduleStamp);

        Assert.False(validForOldStamp);
        Assert.True(validForNewStamp);
    }

    [Fact]
    public async Task Cancelled_IsNotValid()
    {
        var customerId = Guid.NewGuid();
        var appointment = BookedAppointment(customerId);
        var stamp = appointment.ScheduleStamp;
        appointment.Cancel(new AppointmentCancelDto(customerId, true, "No longer needed", Now, null, Ctx, Guid.NewGuid()));
        var repository = new FakeAppointmentRepository();
        repository.Seed(appointment);
        var guard = new AppointmentReminderGuard(repository);

        var valid = await guard.IsStillValidAsync(customerId, appointment.Id, null, null, stamp);

        Assert.False(valid);
    }

    [Fact]
    public async Task DifferentRecipient_IsNotValid()
    {
        var customerId = Guid.NewGuid();
        var appointment = BookedAppointment(customerId);
        var repository = new FakeAppointmentRepository();
        repository.Seed(appointment);
        var guard = new AppointmentReminderGuard(repository);

        var valid = await guard.IsStillValidAsync(Guid.NewGuid(), appointment.Id, null, null, appointment.ScheduleStamp);

        Assert.False(valid);
    }

    [Fact]
    public async Task AppointmentNoLongerExists_IsNotValid()
    {
        var repository = new FakeAppointmentRepository();
        var guard = new AppointmentReminderGuard(repository);

        var valid = await guard.IsStillValidAsync(Guid.NewGuid(), Guid.NewGuid(), null, null, "any-stamp");

        Assert.False(valid);
    }
}
