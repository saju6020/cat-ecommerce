using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C13. Related products must exist; linked products that are not
/// yet active are allowed but never shown until they are (the read side filters).</summary>
public sealed class SetRelatedProductsCommandHandler(
    ILogger<SetRelatedProductsCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository)
    : ICommandHandlerAsync<SetRelatedProductsCommand>
{
    public async Task<CommandResponse> HandleAsync(SetRelatedProductsCommand command)
    {
        logger.LogInformation("Setting related products for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        var inputs = new List<RelatedProductInputDto>();
        foreach (var link in command.Links)
        {
            if (!EnumerationParsing.TryParse<RelatedProductKind>(link.Kind, out var kind) || kind is null)
                return Violation(BusinessViolationCodes.RelatedProductInvalid, nameof(link.Kind), $"'{link.Kind}' is not a known link kind.");

            if (link.RelatedProductId != product.Id && !await productRepository.ExistAsync(p => p.Id == link.RelatedProductId))
                return Violation(BusinessViolationCodes.RelatedProductInvalid, nameof(link.RelatedProductId), $"Product '{link.RelatedProductId}' was not found.");

            inputs.Add(new RelatedProductInputDto(Guid.NewGuid(), link.RelatedProductId, kind, link.SortOrder));
        }

        try
        {
            product.SetRelatedProducts(inputs, command.UserContext, command.CorrelationId);
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
