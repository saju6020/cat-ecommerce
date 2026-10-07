using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Clinic;

/// <summary>HO-APT-001 §8.10/§11.6 (ADR-076). Flags, never cancels; never enqueues (HO-NTF-001
/// ADR-057). One <c>UpdateAsync</c> per affected appointment; idempotent (the aggregate's own
/// <c>FlagForReschedule</c>/<c>ClearRescheduleFlag</c> no-op when already in the target state).</summary>
public sealed class FlagAppointmentsOnLeaveApprovedHandler(
    IFlaggableAppointmentFinder finder, IAggregateRootRepository<AppointmentAggregate> appointmentRepository)
    : IEventHandlerAsync<StaffLeaveApprovedEvent>
{
    public async Task HandleAsync(StaffLeaveApprovedEvent @event)
    {
        foreach (var id in await finder.FindByPerformerInRangeAsync(@event.StaffAccountId, @event.StartsOn, @event.EndsOn))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.FlagForReschedule(new AppointmentRescheduleFlagDto(RescheduleFlagReason.PerformerOnLeave, @event.LeaveRequestId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

public sealed class UnflagAppointmentsOnLeaveCancelledHandler(
    IFlaggableAppointmentFinder finder, IAggregateRootRepository<AppointmentAggregate> appointmentRepository)
    : IEventHandlerAsync<LeaveCancelledEvent>
{
    public async Task HandleAsync(LeaveCancelledEvent @event)
    {
        if (!@event.WasApproved) return;

        foreach (var id in await finder.FindFlaggedBySourceAsync(@event.LeaveRequestId))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.ClearRescheduleFlag(new AppointmentRescheduleUnflagDto(@event.LeaveRequestId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>The event carries no closure id, so the closure is re-matched by branch/dates -- the
/// best identification the event gives (HO-ADM-001's own <c>BranchClosureCreatedEvent</c> shape).</summary>
public sealed class FlagAppointmentsOnClosureHandler(
    IFlaggableAppointmentFinder finder,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchClosureAggregate> closureRepository)
    : IEventHandlerAsync<BranchClosureCreatedEvent>
{
    public async Task HandleAsync(BranchClosureCreatedEvent @event)
    {
        var closure = await closureRepository.GetByFilterAsync(c =>
            c.BranchId == @event.BranchId && c.StartsOn == @event.StartsOn && c.EndsOn == @event.EndsOn && !c.IsCancelled);
        var sourceId = closure?.Id ?? @event.BranchId;

        foreach (var id in await finder.FindByBranchInRangeAsync(@event.BranchId, @event.StartsOn, @event.EndsOn, closure?.PartialFrom, closure?.PartialTo))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.FlagForReschedule(new AppointmentRescheduleFlagDto(RescheduleFlagReason.BranchClosed, sourceId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

public sealed class UnflagAppointmentsOnClosureCancelledHandler(
    IFlaggableAppointmentFinder finder, IAggregateRootRepository<AppointmentAggregate> appointmentRepository)
    : IEventHandlerAsync<BranchClosureCancelledEvent>
{
    public async Task HandleAsync(BranchClosureCancelledEvent @event)
    {
        foreach (var id in await finder.FindFlaggedBySourceAsync(@event.ClosureId))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.ClearRescheduleFlag(new AppointmentRescheduleUnflagDto(@event.ClosureId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

public sealed class FlagAppointmentsOnCatDeceasedHandler(
    IFlaggableAppointmentFinder finder, IAggregateRootRepository<AppointmentAggregate> appointmentRepository, TimeProvider timeProvider)
    : IEventHandlerAsync<CatMarkedDeceasedEvent>
{
    public async Task HandleAsync(CatMarkedDeceasedEvent @event)
    {
        foreach (var id in await finder.FindFutureByCatAsync(@event.CatId, timeProvider.GetUtcNow()))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.FlagForReschedule(new AppointmentRescheduleFlagDto(RescheduleFlagReason.CatDeceased, @event.CatId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

public sealed class FlagAppointmentsOnCatTransferredHandler(
    IFlaggableAppointmentFinder finder, IAggregateRootRepository<AppointmentAggregate> appointmentRepository, TimeProvider timeProvider)
    : IEventHandlerAsync<CatOwnershipTransferredEvent>
{
    public async Task HandleAsync(CatOwnershipTransferredEvent @event)
    {
        foreach (var id in await finder.FindFutureByCatAsync(@event.CatId, timeProvider.GetUtcNow()))
        {
            var appointment = await appointmentRepository.GetByIdAsync(id);
            if (appointment is null) continue;

            appointment.FlagForReschedule(new AppointmentRescheduleFlagDto(RescheduleFlagReason.CatOwnerChanged, @event.CatId, @event.UserContext, @event.CorrelationId));
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
