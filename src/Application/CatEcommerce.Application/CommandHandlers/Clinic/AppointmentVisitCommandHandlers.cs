using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

using static ClinicCommandHandlerSupport;

/// <summary>C8 -- creates the appointment directly as <c>CheckedIn</c> (§8.3).</summary>
public sealed class RegisterWalkInCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RegisterWalkInCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterWalkInCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null || cat.OwnerId != command.CustomerId)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.CatId), ClinicMessages.CatNotBookable);

        var now = timeProvider.GetUtcNow();
        // CODE_REVIEW gate fix (2026-09-28, MEDIUM, fixed alongside the waitlist HIGH): branch-local
        // "today" (ADR-045), not UTC -- matches CheckInAppointmentCommandHandler's own branch lookup.
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        var today = BranchCalendar.LocalDateOf(now, branch?.TimeZoneId ?? "UTC");
        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(command.BranchId, command.ServiceId, today, true, null, null, null));
        var service = inputSet.Service;
        if (service is null || !service.IsActive || service.Price is null)
            return Violation(BusinessViolationCodes.ServiceNotBookable, nameof(command.ServiceId), ClinicMessages.ServiceNotBookable);

        var serviceSnapshot = new ServiceSnapshot(service.Name, service.Category, service.DurationMinutes, service.Price);

        var appointment = new AppointmentAggregate(command.AppointmentId);
        try
        {
            appointment.RegisterWalkIn(new WalkInRegisterDto(
                command.BranchId, command.CustomerId, command.CatId, command.ServiceId, serviceSnapshot,
                AppointmentMapper.ParseEnum<TriagePriority>(command.TriagePriority), command.PresentingComplaint, now,
                command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.SaveAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success(appointment.Id);
    }
}

/// <summary>C9 -- check-in with the branch zone; optional triage.</summary>
public sealed class CheckInAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CheckInAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(CheckInAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        // HO-ADM-001 §8.2 (backlog #8): another branch's appointment is "not found", never refused
        // with a reason that would confirm it exists.
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        var branch = await branchRepository.GetByIdAsync(appointment.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var earlyMinutes = businessSettings.GetInt(SettingKeys.ClinicEarlyCheckInMinutes, appointment.BranchId);
        var now = timeProvider.GetUtcNow();

        try
        {
            appointment.CheckIn(new AppointmentCheckInDto(
                now, timeZoneId, earlyMinutes, AppointmentMapper.ParseNullableEnum<TriagePriority>(command.TriagePriority),
                command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetTriagePriorityCommandHandler(IAggregateRootRepository<AppointmentAggregate> appointmentRepository)
    : ICommandHandlerAsync<SetTriagePriorityCommand>
{
    public async Task<CommandResponse> HandleAsync(SetTriagePriorityCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        // HO-ADM-001 §8.2 (backlog #8): another branch's appointment is "not found", never refused
        // with a reason that would confirm it exists.
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        try
        {
            appointment.SetTriagePriority(new AppointmentTriageDto(AppointmentMapper.ParseEnum<TriagePriority>(command.TriagePriority), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C11 -- a walk-in needs performer + room here; a booked appointment may reassign the
/// room. No exclusion constraint applies to a walk-in (§8.4), so no <c>SlotClaim</c> here -- the
/// aggregate's own qualification check is enough (§8.3).</summary>
public sealed class StartAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    IAggregateRootRepository<RoomAggregate> roomRepository,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> branchAssignmentRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<StartAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(StartAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        // HO-ADM-001 §8.2 (backlog #8): another branch's appointment is "not found", never refused
        // with a reason that would confirm it exists.
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        if (command.PerformerId is { } assignedPerformerId)
        {
            // Backlog #29: the performer must work at the appointment's own branch.
            var assignment = await branchAssignmentRepository.GetByIdAsync(assignedPerformerId);
            if (assignment is null || !assignment.BranchIds.Contains(appointment.BranchId))
                return Violation(BusinessViolationCodes.PerformerNotQualified, nameof(command.PerformerId), "This performer does not work at this branch.");
        }

        if (command.PerformerId is { } performerId)
        {
            var performer = await staffRepository.GetByIdAsync(performerId);
            var service = await serviceRepository.GetByIdAsync(appointment.ClinicServiceId);
            if (performer is null || service is null || !performer.RoleIds.Any(r => service.QualifiedRoleIds.Contains(r, StringComparer.Ordinal)))
                return Violation(BusinessViolationCodes.PerformerNotQualified, nameof(command.PerformerId), "This performer is not qualified for this service.");
        }

        if (command.RoomId is { } roomId)
        {
            var room = await roomRepository.GetByIdAsync(roomId);
            var service = await serviceRepository.GetByIdAsync(appointment.ClinicServiceId);
            if (room is null || room.BranchId != appointment.BranchId)
                return Violation(BusinessViolationCodes.RoomNotAvailable, nameof(command.RoomId), "This room does not belong to this branch.");
            if (!room.IsActive || service is null || room.Kind != service.RequiredRoomKind)
                return Violation(BusinessViolationCodes.RoomKindMismatch, nameof(command.RoomId), "This room is not of the required kind.");
        }

        try
        {
            appointment.Start(new AppointmentStartDto(command.PerformerId, command.RoomId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class CompleteAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository, TimeProvider timeProvider, IAggregateRootRepository<VisitAggregate> visitRepository)
    : ICommandHandlerAsync<CompleteAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(CompleteAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        // HO-ADM-001 §8.2 (backlog #8): another branch's appointment is "not found", never refused
        // with a reason that would confirm it exists.
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        // HO-EMR-001 ADR-133 (guard line only): an appointment whose consultation is still open cannot complete.
        // Finalising the visit does not complete the appointment; the vet completes it after finalising.
        if (await visitRepository.ExistAsync(v => v.AppointmentId == appointment.Id && v.Status == VisitStatus.Open))
            return Violation(BusinessViolationCodes.VisitNotFinalised, nameof(command.AppointmentId), ClinicalMessages.VisitNotFinalised);

        try
        {
            appointment.Complete(new AppointmentCompleteDto(timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class RecordNoShowCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository, IBusinessSettings businessSettings, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordNoShowCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordNoShowCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        // HO-ADM-001 §8.2 (backlog #8): another branch's appointment is "not found", never refused
        // with a reason that would confirm it exists.
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        var graceMinutes = businessSettings.GetInt(SettingKeys.ClinicNoShowGraceMinutes, appointment.BranchId);

        try
        {
            appointment.RecordNoShow(new AppointmentNoShowDto(command.UserContext.UserId, timeProvider.GetUtcNow(), graceMinutes, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
