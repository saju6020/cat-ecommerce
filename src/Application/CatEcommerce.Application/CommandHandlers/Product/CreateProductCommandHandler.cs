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

/// <summary>HO-SHP-001 section 11.4 C1. Slug and (bundle) SKU-code uniqueness pre-checks, backed by
/// the unique indexes and the filter mapping (section 12.5); tax class exists and is active.</summary>
public sealed class CreateProductCommandHandler(
    ILogger<CreateProductCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository)
    : ICommandHandlerAsync<CreateProductCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateProductCommand command)
    {
        logger.LogInformation("Creating product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        if (!EnumerationParsing.TryParse<ProductKind>(command.Kind, out var kind) || kind is null)
            return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.Kind), $"'{command.Kind}' is not a known product kind.");

        if (!EnumerationParsing.TryParse<ProductCategory>(command.Category, out var category) || category is null)
            return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.Category), $"'{command.Category}' is not a known category.");

        ProductSubcategory? subcategory = null;
        if (!string.IsNullOrWhiteSpace(command.Subcategory))
        {
            if (!EnumerationParsing.TryParse<ProductSubcategory>(command.Subcategory, out subcategory) || subcategory is null)
                return Violation(BusinessViolationCodes.InvalidProductCategory, nameof(command.Subcategory), $"'{command.Subcategory}' is not a known subcategory.");
        }

        if (await productRepository.ExistAsync(p => p.Slug == command.Slug))
            return Violation(BusinessViolationCodes.SlugAlreadyInUse, nameof(command.Slug), ShopMessages.SlugAlreadyInUse);

        if (kind == ProductKind.Bundle && !string.IsNullOrWhiteSpace(command.BundleSkuCode) &&
            await productRepository.ExistAsync(p => p.Skus.Any(s => s.Code == command.BundleSkuCode)))
        {
            return Violation(BusinessViolationCodes.SkuCodeAlreadyInUse, nameof(command.BundleSkuCode), ShopMessages.SkuCodeAlreadyInUse);
        }

        var taxClass = await taxClassRepository.GetByIdAsync(command.TaxClassId);
        if (taxClass is null || !taxClass.IsActive)
            return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "The tax class was not found or is not active.");

        var product = new ProductAggregate(command.ProductId);

        try
        {
            product.Create(command.ToDomainDto(kind, category, subcategory));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await productRepository.SaveAsync(product, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = product.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
