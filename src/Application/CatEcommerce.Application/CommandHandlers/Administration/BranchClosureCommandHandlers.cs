using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

public sealed class CreateBranchClosureCommandHandler(
    IAggregateRootRepository<BranchClosureAggregate> closureRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository)
    : ICommandHandlerAsync<CreateBranchClosureCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateBranchClosureCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var closure = new BranchClosureAggregate(command.ClosureId);
        try
        {
            closure.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await closureRepository.SaveAsync(closure, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = closure.Id };
    }
}

/// <summary>"Future" is judged in branch-local date -- the handler loads the branch for its time
/// zone and computes today, then passes that reference date into the aggregate (§11.4).</summary>
public sealed class CancelBranchClosureCommandHandler(
    IAggregateRootRepository<BranchClosureAggregate> closureRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelBranchClosureCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelBranchClosureCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var closure = await closureRepository.GetByIdAsync(command.ClosureId);
        if (closure is null || closure.BranchId != command.BranchId)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.InvalidClosure, nameof(command.ClosureId), "Closure not found.");

        var todayLocal = BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), branch.TimeZoneId);

        try
        {
            closure.Cancel(new BranchClosureCancelDto(todayLocal, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await closureRepository.UpdateAsync(closure, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = closure.Id };
    }
}
