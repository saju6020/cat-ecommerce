using CatEcommerce.Application.CommandHandlers.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Administration;

/// <summary>
/// TEST gate seventh pass: U-C5/U-C7/U-C9, previously BLOCKED. Handler-level, with a fake
/// repository -- the same "hand-written in-memory test double, no mocking library" precedent
/// <c>SetBranchActivationCommandHandlerTests</c> already uses for its own otherwise-hard-to-reach
/// case (I-B4).
/// </summary>
public sealed class LeaveCommandHandlersTests
{
    private static UserContext ContextFor(Guid userId, params string[] roles) => new(userId, Guid.NewGuid()) { Roles = roles };

    private static StaffAccountAggregate CreateStaff(Guid id, string fullName, params string[] roles)
    {
        var staff = new StaffAccountAggregate(id);
        staff.Create(new StaffAccountCreationDto(fullName, MobileNumber.Create("017" + Random.Shared.NextInt64(10_000_000, 99_999_999)), null, roles, TestUserContext.Create(), Guid.NewGuid()));
        return staff;
    }

    private static LeaveRequestAggregate CreateRequestedLeave(Guid staffAccountId, DateOnly startsOn, DateOnly endsOn)
    {
        var leave = new LeaveRequestAggregate(Guid.NewGuid());
        leave.Request(new LeaveRequestCreateDto(staffAccountId, startsOn, endsOn, LeaveKind.Annual, null, ContextFor(staffAccountId, "Nurse"), Guid.NewGuid()));
        return leave;
    }

    /// <summary>U-C5, first half: approving an already-decided (approved) request a second time
    /// returns <c>LeaveNotPending</c>.</summary>
    [Fact]
    public async Task ApproveLeave_AlreadyApproved_ReturnsLeaveNotPending()
    {
        var requester = Guid.NewGuid();
        var leave = CreateRequestedLeave(requester, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(11));

        var leaveRepository = new FakeAggregateRootRepository<LeaveRequestAggregate>();
        leaveRepository.Seed(leave);
        var staffRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var admin = CreateStaff(Guid.NewGuid(), "The Administrator", StaffRoles.Administrator);
        staffRepository.Seed(admin);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new ApproveLeaveCommandHandler(leaveRepository, staffRepository, timeProvider);

        var firstApprove = await handler.HandleAsync(new ApproveLeaveCommand
        {
            LeaveRequestId = leave.Id, UserContext = ContextFor(admin.Id, StaffRoles.Administrator), CorrelationId = Guid.NewGuid()
        });
        Assert.True(firstApprove.IsSuccess);

        var secondApprove = await handler.HandleAsync(new ApproveLeaveCommand
        {
            LeaveRequestId = leave.Id, UserContext = ContextFor(admin.Id, StaffRoles.Administrator), CorrelationId = Guid.NewGuid()
        });

        Assert.False(secondApprove.IsSuccess);
        Assert.Contains(secondApprove.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.LeaveNotPending);
    }

    /// <summary>U-C5, second half: an approved leave whose <c>EndsOn</c> is yesterday cannot be
    /// cancelled.</summary>
    [Fact]
    public async Task CancelLeave_ApprovedAndAlreadyEnded_ReturnsLeaveCannotBeCancelled()
    {
        var requester = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var leave = CreateRequestedLeave(requester, yesterday.AddDays(-2), yesterday);
        leave.Approve(new LeaveDecisionDto(Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(-2), ContextFor(Guid.NewGuid(), StaffRoles.Administrator), Guid.NewGuid()));

        var leaveRepository = new FakeAggregateRootRepository<LeaveRequestAggregate>();
        leaveRepository.Seed(leave);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new CancelLeaveCommandHandler(leaveRepository, timeProvider);

        var admin = Guid.NewGuid();
        var response = await handler.HandleAsync(new CancelLeaveCommand
        {
            LeaveRequestId = leave.Id, UserContext = ContextFor(admin, StaffRoles.Administrator), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.LeaveCannotBeCancelled);
    }

    /// <summary>U-C7: a Nurse requesting leave on behalf of someone else (a non-null,
    /// not-the-caller <c>StaffAccountId</c>) is refused with <c>ActionNotPermitted</c> -- distinct
    /// from <c>NurseApprovingLeave_Returns403</c> (I-C5), which is the *decision* path, refused at
    /// the authorization layer before the handler ever runs; this is the *request* path, refused
    /// inside the handler itself, since <c>Actions.Leave.Request</c> is open to every staff role and
    /// only the handler can tell "for myself" from "for someone else".</summary>
    [Fact]
    public async Task RequestLeave_NurseRequestsForSomeoneElse_ReturnsActionNotPermitted()
    {
        var nurseId = Guid.NewGuid();
        var someoneElseId = Guid.NewGuid();
        var leaveRepository = new FakeAggregateRootRepository<LeaveRequestAggregate>();
        var handler = new RequestLeaveCommandHandler(leaveRepository);

        var response = await handler.HandleAsync(new RequestLeaveCommand
        {
            LeaveRequestId = Guid.NewGuid(), StaffAccountId = someoneElseId,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), EndsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(11),
            Kind = LeaveKind.Annual.DisplayName, UserContext = ContextFor(nurseId, StaffRoles.Nurse), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ActionNotPermitted);
        Assert.Empty(leaveRepository.Saved);
    }

    /// <summary>U-C9: the sole Administrator approving their own leave request is allowed (there is
    /// no one else who could decide it), and the decision note is prefixed
    /// (<c>LeaveRules.SoleAdministratorSelfApprovalPrefix</c>).</summary>
    [Fact]
    public async Task ApproveLeave_SoleAdministratorApprovesOwnRequest_IsAllowedAndNotePrefixed()
    {
        var adminId = Guid.NewGuid();
        var leave = CreateRequestedLeave(adminId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(11));

        var leaveRepository = new FakeAggregateRootRepository<LeaveRequestAggregate>();
        leaveRepository.Seed(leave);
        var staffRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        // The ONLY Administrator in the store -- no other administrator exists to decide instead.
        staffRepository.Seed(CreateStaff(adminId, "The Sole Administrator", StaffRoles.Administrator));

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new ApproveLeaveCommandHandler(leaveRepository, staffRepository, timeProvider);

        var response = await handler.HandleAsync(new ApproveLeaveCommand
        {
            LeaveRequestId = leave.Id, DecisionNote = "Approved by self.", UserContext = ContextFor(adminId, StaffRoles.Administrator), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(LeaveStatus.Approved, leave.Status);
        Assert.StartsWith(LeaveRules.SoleAdministratorSelfApprovalPrefix, leave.DecisionNote);
        Assert.Contains("Approved by self.", leave.DecisionNote);
    }

    /// <summary>Negative control for U-C9: with a SECOND administrator in the store, the same
    /// self-approval attempt is refused instead -- proving the sole-administrator exception really
    /// is about there being no one else, not merely "an administrator approving their own request".</summary>
    [Fact]
    public async Task ApproveLeave_SelfApprovalWithAnotherAdministratorAvailable_ReturnsActionNotPermitted()
    {
        var adminId = Guid.NewGuid();
        var leave = CreateRequestedLeave(adminId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(11));

        var leaveRepository = new FakeAggregateRootRepository<LeaveRequestAggregate>();
        leaveRepository.Seed(leave);
        var staffRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        staffRepository.Seed(CreateStaff(adminId, "Administrator One", StaffRoles.Administrator));
        staffRepository.Seed(CreateStaff(Guid.NewGuid(), "Administrator Two", StaffRoles.Administrator));

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new ApproveLeaveCommandHandler(leaveRepository, staffRepository, timeProvider);

        var response = await handler.HandleAsync(new ApproveLeaveCommand
        {
            LeaveRequestId = leave.Id, UserContext = ContextFor(adminId, StaffRoles.Administrator), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ActionNotPermitted);
        Assert.Equal(LeaveStatus.Requested, leave.Status);
    }
}
