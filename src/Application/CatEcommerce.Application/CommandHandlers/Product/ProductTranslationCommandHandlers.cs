using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C17 -- loads the product, checks <c>ExpectedVersion</c> and passes
/// the enabled locales (configuration) to the aggregate, which decides what is allowed with them.</summary>
public sealed class SetProductTranslationCommandHandler(
    ILogger<SetProductTranslationCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IContentLocaleSettings localeSettings)
    : ICommandHandlerAsync<SetProductTranslationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetProductTranslationCommand command)
    {
        logger.LogInformation("Setting translation {Locale} for product {ProductId}. CorrelationId: {CorrelationId}", command.Locale, command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        try
        {
            product.SetTranslation(
                new ProductTranslationInputDto(command.Locale, command.Name, command.Description, command.IngredientList, command.FeedingGuide),
                localeSettings.DefaultLocale, localeSettings.EnabledLocales, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await productRepository.UpdateAsync(product, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

public sealed class RemoveProductTranslationCommandHandler(
    ILogger<RemoveProductTranslationCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IContentLocaleSettings localeSettings)
    : ICommandHandlerAsync<RemoveProductTranslationCommand>
{
    public async Task<CommandResponse> HandleAsync(RemoveProductTranslationCommand command)
    {
        logger.LogInformation("Removing translation {Locale} from product {ProductId}. CorrelationId: {CorrelationId}", command.Locale, command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        try
        {
            product.RemoveTranslation(command.Locale, localeSettings.DefaultLocale, localeSettings.EnabledLocales, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await productRepository.UpdateAsync(product, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
