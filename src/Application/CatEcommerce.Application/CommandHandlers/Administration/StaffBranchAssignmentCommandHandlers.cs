using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>§11.4: staff account exists (HO-ACC-001 repository); every branch exists and is
/// active.</summary>
public sealed class AssignStaffBranchesCommandHandler(
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignmentRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository)
    : ICommandHandlerAsync<AssignStaffBranchesCommand>
{
    public async Task<CommandResponse> HandleAsync(AssignStaffBranchesCommand command)
    {
        var staff = await staffRepository.GetByIdAsync(command.StaffAccountId);
        if (staff is null)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.RoleRequired, nameof(command.StaffAccountId), "Staff account not found.");

        foreach (var branchId in command.BranchIds.Distinct())
        {
            var branch = await branchRepository.GetByIdAsync(branchId);
            if (branch is null || !branch.IsActive)
                return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchIds), $"Branch '{branchId}' was not found or is not active.");
        }

        var assignment = await assignmentRepository.GetByIdAsync(command.StaffAccountId);
        var isNew = assignment is null;
        assignment ??= new StaffBranchAssignmentAggregate(command.StaffAccountId);

        try
        {
            assignment.Assign(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew)
            await assignmentRepository.SaveAsync(assignment, EventPublishOption.InMemoryPublishOnly);
        else
            await assignmentRepository.UpdateAsync(assignment, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = assignment.Id };
    }
}
