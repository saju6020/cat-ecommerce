using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>§11.4 (C7): <c>StaffAccountId</c> null -&gt; the caller; non-null and &#8800; caller -&gt;
/// caller must hold Administrator; overlap pre-check against the person's Requested/Approved leave.</summary>
public sealed class RequestLeaveCommandHandler(
    IAggregateRootRepository<LeaveRequestAggregate> leaveRepository)
    : ICommandHandlerAsync<RequestLeaveCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestLeaveCommand command)
    {
        var staffAccountId = command.StaffAccountId ?? command.UserContext.UserId;

        if (command.StaffAccountId is not null && command.StaffAccountId != command.UserContext.UserId &&
            !command.UserContext.Roles.Contains(StaffRoles.Administrator, StringComparer.Ordinal))
        {
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ActionNotPermitted, nameof(command.StaffAccountId), "Only an administrator may request leave on behalf of someone else.");
        }

        var overlaps = await leaveRepository.ExistAsync(l =>
            l.StaffAccountId == staffAccountId &&
            (l.Status == LeaveStatus.Requested || l.Status == LeaveStatus.Approved) &&
            l.StartsOn <= command.EndsOn && command.StartsOn <= l.EndsOn);
        if (overlaps)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.LeaveOverlapsExistingLeave, nameof(command.StartsOn), "This period overlaps an existing leave request.");

        var leave = new LeaveRequestAggregate(command.LeaveRequestId);
        try
        {
            leave.Request(command.ToDomainDto(staffAccountId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await leaveRepository.SaveAsync(leave, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = leave.Id };
    }
}

/// <summary>Shared decider rule (OQ-3): decider &#8800; requester, unless the decider is the only
/// staff account holding Administrator -- then allowed, and the decision note is prefixed.</summary>
internal static class LeaveDecisionRules
{
    public static async Task<(bool Allowed, bool SelfApproved)> CheckAsync(
        IAggregateRootRepository<StaffAccountAggregate> staffRepository, Guid deciderId, Guid requesterId)
    {
        if (deciderId != requesterId) return (true, false);

        var anotherAdministratorExists = await staffRepository.ExistAsync(s => s.Id != deciderId && s.RoleIds.Contains(StaffRoles.Administrator));
        return anotherAdministratorExists ? (false, false) : (true, true);
    }

    public static string? ApplyPrefix(string? note, bool selfApproved) =>
        selfApproved ? $"{LeaveRules.SoleAdministratorSelfApprovalPrefix} {note}".Trim() : note;
}

public sealed class ApproveLeaveCommandHandler(
    IAggregateRootRepository<LeaveRequestAggregate> leaveRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApproveLeaveCommand>
{
    public async Task<CommandResponse> HandleAsync(ApproveLeaveCommand command)
    {
        var leave = await leaveRepository.GetByIdAsync(command.LeaveRequestId);
        if (leave is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.InvalidLeavePeriod, nameof(command.LeaveRequestId), "Leave request not found.");

        var (allowed, selfApproved) = await LeaveDecisionRules.CheckAsync(staffRepository, command.UserContext.UserId, leave.StaffAccountId);
        if (!allowed) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ActionNotPermitted, nameof(command.LeaveRequestId), "You cannot decide your own leave request.");

        try
        {
            leave.Approve(new LeaveDecisionDto(command.UserContext.UserId, LeaveDecisionRules.ApplyPrefix(command.DecisionNote, selfApproved), timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await leaveRepository.UpdateAsync(leave, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = leave.Id };
    }
}

public sealed class RejectLeaveCommandHandler(
    IAggregateRootRepository<LeaveRequestAggregate> leaveRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RejectLeaveCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectLeaveCommand command)
    {
        var leave = await leaveRepository.GetByIdAsync(command.LeaveRequestId);
        if (leave is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.InvalidLeavePeriod, nameof(command.LeaveRequestId), "Leave request not found.");

        var (allowed, selfApproved) = await LeaveDecisionRules.CheckAsync(staffRepository, command.UserContext.UserId, leave.StaffAccountId);
        if (!allowed) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ActionNotPermitted, nameof(command.LeaveRequestId), "You cannot decide your own leave request.");

        try
        {
            leave.Reject(new LeaveDecisionDto(command.UserContext.UserId, LeaveDecisionRules.ApplyPrefix(command.DecisionNote, selfApproved), timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await leaveRepository.UpdateAsync(leave, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = leave.Id };
    }
}

/// <summary>Requested -&gt; by requester or administrator; Approved -&gt; administrator only, and
/// only if EndsOn is today or later (§8.4).</summary>
public sealed class CancelLeaveCommandHandler(IAggregateRootRepository<LeaveRequestAggregate> leaveRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelLeaveCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelLeaveCommand command)
    {
        var leave = await leaveRepository.GetByIdAsync(command.LeaveRequestId);
        if (leave is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.InvalidLeavePeriod, nameof(command.LeaveRequestId), "Leave request not found.");

        var isAdministrator = command.UserContext.Roles.Contains(StaffRoles.Administrator, StringComparer.Ordinal);
        var isRequester = leave.StaffAccountId == command.UserContext.UserId;

        if (leave.Status == LeaveStatus.Approved && !isAdministrator)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ActionNotPermitted, nameof(command.LeaveRequestId), "Only an administrator can cancel an approved leave.");

        if (leave.Status == LeaveStatus.Requested && !isRequester && !isAdministrator)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ActionNotPermitted, nameof(command.LeaveRequestId), "You may only cancel your own leave request.");

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            leave.Cancel(new LeaveCancelDto(today, command.UserContext.UserId, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await leaveRepository.UpdateAsync(leave, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = leave.Id };
    }
}
