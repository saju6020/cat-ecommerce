using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Vaccination;

public sealed class RegisterVaccineCommandHandler(
    ILogger<RegisterVaccineCommandHandler> logger,
    IAggregateRootRepository<VaccineAggregate> vaccineRepository)
    : ICommandHandlerAsync<RegisterVaccineCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterVaccineCommand command)
    {
        logger.LogInformation("Registering vaccine {Code}. CorrelationId: {CorrelationId}", command.Code, command.CorrelationId);
        // A uniqueness lookup is not domain logic -- handler, via repository.
        // .ai/command-handler-pattern.md section 4.
        var codeExists = await vaccineRepository.ExistAsync(v => v.Code == command.Code);
        if (codeExists)
            return Violation(BusinessViolationCodes.VaccineCodeAlreadyExists, nameof(command.Code),
                $"A vaccine with code '{command.Code}' already exists.");

        var vaccine = new VaccineAggregate(command.VaccineId);

        try
        {
            vaccine.Register(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await vaccineRepository.SaveAsync(vaccine, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = vaccine.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(VaccineAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
