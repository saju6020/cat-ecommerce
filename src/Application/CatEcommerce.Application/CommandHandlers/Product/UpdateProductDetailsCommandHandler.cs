using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C2. ExpectedVersion; tax class check only when changed;
/// country-code validity is the aggregate's own rule.</summary>
public sealed class UpdateProductDetailsCommandHandler(
    ILogger<UpdateProductDetailsCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository)
    : ICommandHandlerAsync<UpdateProductDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateProductDetailsCommand command)
    {
        logger.LogInformation("Updating product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        if (!EnumerationParsing.TryParse<ProductCategory>(command.Category, out var category) || category is null)
            return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.Category), $"'{command.Category}' is not a known category.");

        ProductSubcategory? subcategory = null;
        if (!string.IsNullOrWhiteSpace(command.Subcategory))
        {
            if (!EnumerationParsing.TryParse<ProductSubcategory>(command.Subcategory, out subcategory) || subcategory is null)
                return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.Subcategory), $"'{command.Subcategory}' is not a known subcategory.");
        }

        var healthFocus = new List<HealthFocus>();
        foreach (var raw in command.HealthFocus)
        {
            if (!EnumerationParsing.TryParse<HealthFocus>(raw, out var focus) || focus is null)
                return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.HealthFocus), $"'{raw}' is not a known health focus.");
            healthFocus.Add(focus);
        }

        if (command.TaxClassId != product.TaxClassId)
        {
            var taxClass = await taxClassRepository.GetByIdAsync(command.TaxClassId);
            if (taxClass is null || !taxClass.IsActive)
                return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "The tax class was not found or is not active.");
        }

        try
        {
            product.UpdateDetails(command.ToDomainDto(category, subcategory, healthFocus));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await productRepository.UpdateAsync(product, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = product.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
