using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

using static ClinicCommandHandlerSupport;

/// <summary>C6 -- staff booking skips the hold (ADR-072); same slot rules in staff mode.</summary>
public sealed class BookAppointmentByStaffCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    IBusinessSettings businessSettings,
    IStaleHoldReleaser staleHoldReleaser,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<BookAppointmentByStaffCommand>
{
    public async Task<CommandResponse> HandleAsync(BookAppointmentByStaffCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null || cat.OwnerId != command.CustomerId)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.CatId), ClinicMessages.CatNotBookable);

        var now = timeProvider.GetUtcNow();
        var outcome = await SlotClaim.ValidateAsync(
            new SlotClaim.Request(command.BranchId, command.ServiceId, command.StartsOn, command.PerformerId, command.CatId, null, IsStaff: true, now, command.RoomId),
            branchRepository, schedulingInputs, businessSettings);

        if (!outcome.IsSuccess)
            return Violation(outcome.ErrorCode!, nameof(command.StartsOn), outcome.Message!);

        var slot = outcome.Slot!;
        var service = outcome.Inputs!.Service!;
        var serviceSnapshot = new ServiceSnapshot(service.Name, service.Category, service.DurationMinutes, service.Price!);

        // ADR-070/§11.4 step 3: a stale hold overlapping this slot must not 23P01 a staff booking.
        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, slot, command.CatId, now, command.AppointmentId);

        var appointment = new AppointmentAggregate(command.AppointmentId);
        try
        {
            appointment.BookDirect(new DirectBookingDto(
                command.BranchId, command.CustomerId, command.CatId, command.ServiceId, serviceSnapshot,
                slot.PerformerId, slot.RoomId, slot.StartsOn, slot.EndsOn, command.ReasonForVisit,
                command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var ctx = await ClinicNotifications.LoadContextAsync(branchRepository, catRepository, staffRepository, appointment);
        ClinicNotifications.EnqueueConfirmed(notificationOutbox, linkBuilder, appointment, ctx, command.CorrelationId, command.UserContext);
        ClinicNotifications.EnqueueReminders(notificationOutbox, linkBuilder, businessSettings, appointment, ctx, now, command.CorrelationId, command.UserContext);

        await appointmentRepository.SaveAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success(appointment.Id);
    }
}

/// <summary>C7 -- <c>ExpectedVersion</c> guards a stale calendar: a <c>409</c> via
/// <c>BusinessViolationCodes.StaleCalendarVersion</c> (the controller maps it to <c>Conflict</c>).
/// Checked against <c>appointment.Version</c> before <c>Reschedule</c> runs, not via
/// <c>EfAggregateRootRepository.UpdateAsync(aggregate, expectedVersion, ...)</c> afterwards: that
/// overload compares <paramref name="command"/>'s <c>ExpectedVersion</c> against the aggregate's
/// <em>current, in-memory</em> <c>Version</c> -- which <c>Reschedule</c> has already bumped via its
/// own <c>UpdateVersion()</c> (§11.4/command-handler-pattern.md §9) by the time <c>UpdateAsync</c>
/// runs, so the two could never match and every move would 409 regardless of a stale calendar
/// (gate fix, MEDIUM). A handler reports an expected outcome by returning a failure, never by
/// throwing (§6/command-handler-pattern.md), so this is a <c>Violation</c>, not a raised
/// <c>DbUpdateConcurrencyException</c>.</summary>
public sealed class MoveAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    IBusinessSettings businessSettings,
    IStaleHoldReleaser staleHoldReleaser,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<MoveAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(MoveAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        // Gate fix: the version guard has to run against the version the caller actually loaded,
        // before Reschedule bumps it -- see the type's remarks.
        if (appointment.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleCalendarVersion, nameof(command.ExpectedVersion), "This appointment has changed since you loaded it. Reload and try again.");

        var now = timeProvider.GetUtcNow();
        var outcome = await SlotClaim.ValidateAsync(
            new SlotClaim.Request(appointment.BranchId, appointment.ClinicServiceId, command.StartsOn, command.PerformerId, appointment.CatId, appointment.Id, IsStaff: true, now, command.RoomId),
            branchRepository, schedulingInputs, businessSettings);

        if (!outcome.IsSuccess)
            return Violation(outcome.ErrorCode!, nameof(command.StartsOn), outcome.Message!);

        var slot = outcome.Slot!;
        var previousStartsOn = appointment.StartsOn!.Value;

        // ADR-070/§11.4 step 3: a stale hold overlapping the destination slot must not 23P01 a move.
        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, slot, appointment.CatId, now, appointment.Id);

        try
        {
            appointment.Reschedule(new AppointmentRescheduleDto(slot.StartsOn, slot.EndsOn, slot.PerformerId, slot.RoomId, now, null, IsStaff: true, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (appointment.StartsOn != previousStartsOn)
        {
            var ctx = await ClinicNotifications.LoadContextAsync(branchRepository, catRepository, staffRepository, appointment);
            ClinicNotifications.EnqueueRescheduled(notificationOutbox, linkBuilder, appointment, ctx, previousStartsOn, command.CorrelationId, command.UserContext);
            ClinicNotifications.EnqueueReminders(notificationOutbox, linkBuilder, businessSettings, appointment, ctx, now, command.CorrelationId, command.UserContext);
        }

        // The stale-calendar guard already ran above, against the version the caller loaded; the
        // exclusion constraints still protect the actual time (§12.1).
        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
