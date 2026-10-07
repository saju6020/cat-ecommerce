using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>§11.4 (C5/C6): staff account exists, has a rosterable role, is assigned to the branch
/// (or the sole-branch rule applies); every <c>DefaultRoomId</c> belongs to the branch;
/// <c>EffectiveFrom</c> strictly after today in branch time; load-or-create the roster by the
/// unique pair. <see cref="StaffRosterAggregate.PromoteDuePattern"/> is called lazily on load
/// (ADR-047) before any other mutation.</summary>
public sealed class SetRosterPatternCommandHandler(
    IAggregateRootRepository<StaffRosterAggregate> rosterRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignmentRepository,
    IAggregateRootRepository<RoomAggregate> roomRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetRosterPatternCommand>
{
    public async Task<CommandResponse> HandleAsync(SetRosterPatternCommand command)
    {
        var staff = await staffRepository.GetByIdAsync(command.StaffAccountId);
        if (staff is null || !staff.RoleIds.Intersect(StaffRosterRules.RosterableRoles, StringComparer.Ordinal).Any())
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.StaffNotRosterable, nameof(command.StaffAccountId), "This staff member does not hold a rosterable role.");

        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var assignmentError = await CheckBranchAssignmentAsync(command.StaffAccountId, command.BranchId);
        if (assignmentError is not null) return assignmentError;

        foreach (var roomId in command.Slots.Select(s => s.DefaultRoomId).Where(id => id is not null).Select(id => id!.Value).Distinct())
        {
            var room = await roomRepository.GetByIdAsync(roomId);
            if (room is null || room.BranchId != command.BranchId)
                return CreateBranchCommandHandler.Violation(BusinessViolationCodes.RoomNotInBranch, nameof(command.Slots), $"Room '{roomId}' does not belong to this branch.");
        }

        var roster = await rosterRepository.GetByFilterAsync(r => r.StaffAccountId == command.StaffAccountId && r.BranchId == command.BranchId);
        var isNew = roster is null;
        roster ??= new StaffRosterAggregate(Guid.NewGuid());

        var today = BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), branch.TimeZoneId);

        if (isNew) roster.Open(command.StaffAccountId, command.BranchId, command.UserContext);
        roster.PromoteDuePattern(new RosterPatternPromoteDto(today, command.UserContext, Guid.NewGuid()));

        try
        {
            if (command.EffectiveFrom is null)
                roster.SetCurrentPattern(command.ToCurrentPatternDto(today));
            else
                roster.ScheduleNextPattern(command.ToNextPatternDto(today));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew) await rosterRepository.SaveAsync(roster, EventPublishOption.InMemoryPublishOnly);
        else await rosterRepository.UpdateAsync(roster, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = roster.Id };
    }

    /// <summary>ADR-046's sole-branch rule, reused here (§11.4): assigned explicitly, or the only
    /// active branch exists and it is this one.</summary>
    private async Task<CommandResponse?> CheckBranchAssignmentAsync(Guid staffAccountId, Guid branchId)
    {
        var assignment = await assignmentRepository.GetByIdAsync(staffAccountId);
        if (assignment is not null && assignment.BranchIds.Contains(branchId)) return null;

        var otherActiveExists = await branchRepository.ExistAsync(b => b.IsActive && b.Id != branchId);
        if (!otherActiveExists) return null;

        return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotAssigned, nameof(branchId), "This staff member is not assigned to this branch.");
    }
}

public sealed class CancelScheduledRosterPatternCommandHandler(IAggregateRootRepository<StaffRosterAggregate> rosterRepository)
    : ICommandHandlerAsync<CancelScheduledRosterPatternCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelScheduledRosterPatternCommand command)
    {
        var roster = await rosterRepository.GetByFilterAsync(r => r.StaffAccountId == command.StaffAccountId && r.BranchId == command.BranchId);
        if (roster is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.StaffAccountId), "Roster not found.");

        roster.CancelNextPattern(new RosterNextPatternCancelDto(command.UserContext, command.CorrelationId));

        await rosterRepository.UpdateAsync(roster, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = roster.Id };
    }
}

public sealed class CreateRosterExceptionCommandHandler(
    IAggregateRootRepository<RosterExceptionAggregate> exceptionRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository)
    : ICommandHandlerAsync<CreateRosterExceptionCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateRosterExceptionCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var exception = new RosterExceptionAggregate(command.RosterExceptionId);
        try
        {
            exception.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await exceptionRepository.SaveAsync(exception, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = exception.Id };
    }
}

public sealed class CancelRosterExceptionCommandHandler(
    IAggregateRootRepository<RosterExceptionAggregate> exceptionRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelRosterExceptionCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelRosterExceptionCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var exception = await exceptionRepository.GetByIdAsync(command.RosterExceptionId);
        if (exception is null || exception.BranchId != command.BranchId)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.InvalidShiftSlot, nameof(command.RosterExceptionId), "Roster exception not found.");

        var today = BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), branch.TimeZoneId);

        try
        {
            exception.Cancel(new RosterExceptionCancelDto(today, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await exceptionRepository.UpdateAsync(exception, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = exception.Id };
    }
}
