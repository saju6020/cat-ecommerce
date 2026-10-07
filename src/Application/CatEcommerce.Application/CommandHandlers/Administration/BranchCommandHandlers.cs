using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>HO-ADM-001 §11.4 (C1). The handler shape throughout this file: load, mutate one
/// aggregate, save; cross-record checks (code uniqueness, "last active branch") happen here.</summary>
public sealed class CreateBranchCommandHandler(IAggregateRootRepository<BranchAggregate> repository) : ICommandHandlerAsync<CreateBranchCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateBranchCommand command)
    {
        var normalisedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.ExistAsync(b => b.Code == normalisedCode))
            return Violation(BusinessViolationCodes.BranchCodeAlreadyExists, nameof(command.Code), $"Branch code '{command.Code}' is already in use.");

        var branch = new BranchAggregate(command.BranchId);
        try
        {
            branch.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.SaveAsync(branch, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = branch.Id };
    }

    internal static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(BranchAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

public sealed class UpdateBranchDetailsCommandHandler(IAggregateRootRepository<BranchAggregate> repository) : ICommandHandlerAsync<UpdateBranchDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateBranchDetailsCommand command)
    {
        var branch = await repository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        try
        {
            branch.UpdateDetails(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.UpdateAsync(branch, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = branch.Id };
    }
}

public sealed class SetBranchWeeklyHoursCommandHandler(IAggregateRootRepository<BranchAggregate> repository) : ICommandHandlerAsync<SetBranchWeeklyHoursCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBranchWeeklyHoursCommand command)
    {
        var branch = await repository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        try
        {
            branch.SetWeeklyHours(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.UpdateAsync(branch, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = branch.Id };
    }
}

/// <summary>Deactivating: counts other active branches -- zero -> LastActiveBranchCannotBeDeactivated
/// (§11.4).</summary>
public sealed class SetBranchActivationCommandHandler(IAggregateRootRepository<BranchAggregate> repository) : ICommandHandlerAsync<SetBranchActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBranchActivationCommand command)
    {
        var branch = await repository.GetByIdAsync(command.BranchId);
        if (branch is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        if (!command.IsActive && branch.IsActive)
        {
            var anotherActiveExists = await repository.ExistAsync(b => b.IsActive && b.Id != command.BranchId);
            if (!anotherActiveExists)
                return CreateBranchCommandHandler.Violation(BusinessViolationCodes.LastActiveBranchCannotBeDeactivated, nameof(command.IsActive), "The last active branch cannot be deactivated.");
        }

        branch.SetActivation(command.ToDomainDto());

        await repository.UpdateAsync(branch, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = branch.Id };
    }
}
