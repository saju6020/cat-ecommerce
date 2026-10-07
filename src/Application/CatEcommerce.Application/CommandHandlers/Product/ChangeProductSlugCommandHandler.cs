using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C3.</summary>
public sealed class ChangeProductSlugCommandHandler(
    ILogger<ChangeProductSlugCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository)
    : ICommandHandlerAsync<ChangeProductSlugCommand>
{
    public async Task<CommandResponse> HandleAsync(ChangeProductSlugCommand command)
    {
        logger.LogInformation("Changing slug for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        if (await productRepository.ExistAsync(p => p.Id != command.ProductId && p.Slug == command.Slug))
            return Violation(BusinessViolationCodes.SlugAlreadyInUse, nameof(command.Slug), ShopMessages.SlugAlreadyInUse);

        try
        {
            product.ChangeSlug(command.Slug, command.UserContext, command.CorrelationId);
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
