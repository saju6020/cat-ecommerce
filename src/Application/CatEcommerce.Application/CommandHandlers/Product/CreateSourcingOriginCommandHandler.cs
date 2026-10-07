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

/// <summary>HO-SHP-001 section 11.4 C9. Country code validity and uniqueness pre-check.</summary>
public sealed class CreateSourcingOriginCommandHandler(
    ILogger<CreateSourcingOriginCommandHandler> logger,
    IAggregateRootRepository<SourcingOriginAggregate> originRepository)
    : ICommandHandlerAsync<CreateSourcingOriginCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateSourcingOriginCommand command)
    {
        logger.LogInformation("Creating sourcing origin {CountryCode}. CorrelationId: {CorrelationId}", command.CountryCode, command.CorrelationId);

        if (!CountryCodes.IsValid(command.CountryCode))
            return Violation(BusinessViolationCodes.InvalidCountryCode, nameof(command.CountryCode), $"'{command.CountryCode}' is not a known country code.");

        var normalised = CountryCodes.Normalise(command.CountryCode);
        if (await originRepository.ExistAsync(o => o.CountryCode == normalised))
            return Violation(BusinessViolationCodes.OriginAlreadyExists, nameof(command.CountryCode), ShopMessages.OriginAlreadyExists);

        var origin = new SourcingOriginAggregate(Guid.NewGuid());

        try
        {
            origin.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await originRepository.SaveAsync(origin, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = origin.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(SourcingOriginAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
