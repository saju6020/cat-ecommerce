using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

public sealed class CreateTaxClassCommandHandler(IAggregateRootRepository<TaxClassAggregate> repository) : ICommandHandlerAsync<CreateTaxClassCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateTaxClassCommand command)
    {
        if (await repository.ExistAsync(t => t.Code == command.Code))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ServiceCodeAlreadyExists, nameof(command.Code), $"Tax class code '{command.Code}' is already in use.");

        var taxClass = new TaxClassAggregate(command.TaxClassId);
        try
        {
            taxClass.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.SaveAsync(taxClass, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = taxClass.Id };
    }
}

public sealed class UpdateTaxClassCommandHandler(IAggregateRootRepository<TaxClassAggregate> repository) : ICommandHandlerAsync<UpdateTaxClassCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateTaxClassCommand command)
    {
        var taxClass = await repository.GetByIdAsync(command.TaxClassId);
        if (taxClass is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found.");

        try
        {
            taxClass.Update(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.UpdateAsync(taxClass, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = taxClass.Id };
    }
}

public sealed class SetTaxRateCommandHandler(
    IAggregateRootRepository<TaxRateAggregate> rateRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetTaxRateCommand>
{
    public async Task<CommandResponse> HandleAsync(SetTaxRateCommand command)
    {
        if (!await taxClassRepository.ExistAsync(t => t.Id == command.TaxClassId))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found.");

        if (await rateRepository.ExistAsync(r => r.TaxClassId == command.TaxClassId && r.EffectiveFrom == command.EffectiveFrom))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.DuplicatePriceEffectiveDate, nameof(command.EffectiveFrom), "A rate already exists with this effective date.");

        var rate = new TaxRateAggregate(command.TaxRateId);
        try
        {
            rate.Create(command.ToDomainDto(timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await rateRepository.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = rate.Id };
    }
}
