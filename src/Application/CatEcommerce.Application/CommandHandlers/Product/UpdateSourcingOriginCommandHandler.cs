using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C9.</summary>
public sealed class UpdateSourcingOriginCommandHandler(
    ILogger<UpdateSourcingOriginCommandHandler> logger,
    IAggregateRootRepository<SourcingOriginAggregate> originRepository)
    : ICommandHandlerAsync<UpdateSourcingOriginCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateSourcingOriginCommand command)
    {
        logger.LogInformation("Updating sourcing origin {CountryCode}. CorrelationId: {CorrelationId}", command.CountryCode, command.CorrelationId);

        if (!CountryCodes.IsValid(command.CountryCode))
            return Violation(BusinessViolationCodes.InvalidCountryCode, nameof(command.CountryCode), $"'{command.CountryCode}' is not a known country code.");

        var normalised = CountryCodes.Normalise(command.CountryCode);
        var origin = await originRepository.GetByFilterAsync(o => o.CountryCode == normalised);
        if (origin is null)
            return Violation(BusinessViolationCodes.OriginNotFound, nameof(command.CountryCode), $"No origin exists for '{command.CountryCode}'.");

        try
        {
            origin.Update(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await originRepository.UpdateAsync(origin, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(SourcingOriginAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
