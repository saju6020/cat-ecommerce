using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>§11.4 (C8): TaxClassId exists; every role in StaffRoles.All (aggregate-checked); code
/// unique.</summary>
public sealed class CreateClinicServiceCommandHandler(
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository)
    : ICommandHandlerAsync<CreateClinicServiceCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateClinicServiceCommand command)
    {
        if (!await taxClassRepository.ExistAsync(t => t.Id == command.TaxClassId))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found.");

        if (await serviceRepository.ExistAsync(s => s.Code == command.Code))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ServiceCodeAlreadyExists, nameof(command.Code), $"Service code '{command.Code}' is already in use.");

        var service = new ClinicServiceAggregate(command.ServiceId);
        try
        {
            service.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await serviceRepository.SaveAsync(service, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = service.Id };
    }
}

public sealed class UpdateClinicServiceCommandHandler(
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository)
    : ICommandHandlerAsync<UpdateClinicServiceCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateClinicServiceCommand command)
    {
        var service = await serviceRepository.GetByIdAsync(command.ServiceId);
        if (service is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ServiceId), "Service not found.");

        if (!await taxClassRepository.ExistAsync(t => t.Id == command.TaxClassId))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found.");

        try
        {
            service.Update(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await serviceRepository.UpdateAsync(service, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = service.Id };
    }
}

public sealed class SetClinicServiceActivationCommandHandler(IAggregateRootRepository<ClinicServiceAggregate> serviceRepository)
    : ICommandHandlerAsync<SetClinicServiceActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetClinicServiceActivationCommand command)
    {
        var service = await serviceRepository.GetByIdAsync(command.ServiceId);
        if (service is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ServiceId), "Service not found.");

        service.SetActivation(command.ToDomainDto());

        await serviceRepository.UpdateAsync(service, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = service.Id };
    }
}

/// <summary>§11.4: service exists; EffectiveFrom &gt;= now (TimeProvider); no existing row with the
/// same EffectiveFrom.</summary>
public sealed class SetServicePriceCommandHandler(
    IAggregateRootRepository<ServicePriceAggregate> priceRepository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetServicePriceCommand>
{
    public async Task<CommandResponse> HandleAsync(SetServicePriceCommand command)
    {
        if (!await serviceRepository.ExistAsync(s => s.Id == command.ServiceId))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ServiceId), "Service not found.");

        if (await priceRepository.ExistAsync(p => p.ClinicServiceId == command.ServiceId && p.EffectiveFrom == command.EffectiveFrom))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.DuplicatePriceEffectiveDate, nameof(command.EffectiveFrom), "A price already exists with this effective date.");

        var price = new ServicePriceAggregate(command.PriceId);
        try
        {
            price.Create(command.ToDomainDto(timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await priceRepository.SaveAsync(price, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = price.Id };
    }
}
