using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

using static ClinicCommandHandlerSupport;

/// <summary>C4 -- §8.6/§11.4. Excludes the appointment's own busy time (U-B12) via
/// <c>SlotClaim.Request.ExcludeAppointmentId</c>.</summary>
public sealed class RescheduleAppointmentCommandHandler(
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
    : ICommandHandlerAsync<RescheduleAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(RescheduleAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || !await IsCurrentOwnerAsync(catRepository, appointment, command.UserContext.UserId))
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        var now = timeProvider.GetUtcNow();
        var outcome = await SlotClaim.ValidateAsync(
            new SlotClaim.Request(appointment.BranchId, appointment.ClinicServiceId, command.StartsOn, command.PerformerId, appointment.CatId, appointment.Id, IsStaff: false, now),
            branchRepository, schedulingInputs, businessSettings);

        if (!outcome.IsSuccess)
        {
            var forCustomer = SlotClaim.ForCustomer(outcome);
            return Violation(forCustomer.ErrorCode!, nameof(command.StartsOn), forCustomer.Message!);
        }

        var slot = outcome.Slot!;
        var previousStartsOn = appointment.StartsOn!.Value;

        // ADR-070/§11.4 step 3: a stale hold overlapping the new time must not 23P01 a slot the
        // customer was just shown as free.
        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, slot, appointment.CatId, now, appointment.Id);
        var cutoffHours = businessSettings.GetInt(SettingKeys.BookingChangeCutoffHours, appointment.BranchId);

        try
        {
            appointment.Reschedule(new AppointmentRescheduleDto(slot.StartsOn, slot.EndsOn, slot.PerformerId, slot.RoomId, now, cutoffHours, IsStaff: false, command.UserContext, command.CorrelationId));
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

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C5 -- customer: own, cut-off. Staff: <c>Appointments.Book</c>, any time, reason
/// required (validator). Enqueues the cancellation notice either way.</summary>
public sealed class CancelAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IBusinessSettings businessSettings,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelAppointmentCommand command)
    {
        var isStaff = command.IsStaff;

        // Staff: only an appointment of their active branch (backlog #8, fail closed when the
        // controller supplied none). Customer: only their own, and only while they still own the
        // cat (backlog #25).
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null
            || (isStaff && appointment.BranchId != command.BranchId)
            || (!isStaff && !await IsCurrentOwnerAsync(catRepository, appointment, command.UserContext.UserId)))
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        var now = timeProvider.GetUtcNow();
        var cutoffHours = isStaff ? (int?)null : businessSettings.GetInt(SettingKeys.BookingChangeCutoffHours, appointment.BranchId);

        try
        {
            appointment.Cancel(new AppointmentCancelDto(command.UserContext.UserId, !isStaff, command.Reason, now, cutoffHours, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var ctx = await ClinicNotifications.LoadContextAsync(branchRepository, catRepository, staffRepository, appointment);
        ClinicNotifications.EnqueueCancelled(notificationOutbox, linkBuilder, appointment, ctx, cancelledByClinic: isStaff, command.CorrelationId, command.UserContext);

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
