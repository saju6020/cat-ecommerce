using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C8.</summary>
public sealed class SetFoodDetailsCommandHandler(
    ILogger<SetFoodDetailsCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository)
    : ICommandHandlerAsync<SetFoodDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(SetFoodDetailsCommand command)
    {
        logger.LogInformation("Setting food details for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        LifeStage? lifeStage = null;
        if (!string.IsNullOrWhiteSpace(command.LifeStage))
        {
            if (!EnumerationParsing.TryParse<LifeStage>(command.LifeStage, out lifeStage) || lifeStage is null)
                return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.LifeStage), $"'{command.LifeStage}' is not a known life stage.");
        }

        ProteinSource? proteinSource = null;
        if (!string.IsNullOrWhiteSpace(command.ProteinSource))
        {
            if (!EnumerationParsing.TryParse<ProteinSource>(command.ProteinSource, out proteinSource) || proteinSource is null)
                return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.ProteinSource), $"'{command.ProteinSource}' is not a known protein source.");
        }

        try
        {
            product.SetFoodDetails(command.ToDomainDto(lifeStage, proteinSource), command.UserContext, command.CorrelationId);
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
