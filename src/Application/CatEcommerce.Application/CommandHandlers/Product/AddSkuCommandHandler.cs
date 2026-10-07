using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C4. Code/barcode uniqueness pre-checks across all products
/// (repository filter on the owned table); the unique indexes back this up.</summary>
public sealed class AddSkuCommandHandler(
    ILogger<AddSkuCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository)
    : ICommandHandlerAsync<AddSkuCommand>
{
    public async Task<CommandResponse> HandleAsync(AddSkuCommand command)
    {
        logger.LogInformation("Adding SKU to product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        if (await productRepository.ExistAsync(p => p.Skus.Any(s => s.Code == command.Code)))
            return Violation(BusinessViolationCodes.SkuCodeAlreadyInUse, nameof(command.Code), ShopMessages.SkuCodeAlreadyInUse);

        if (!string.IsNullOrWhiteSpace(command.Barcode) &&
            await productRepository.ExistAsync(p => p.Skus.Any(s => s.Barcode == command.Barcode)))
        {
            return Violation(BusinessViolationCodes.BarcodeAlreadyInUse, nameof(command.Barcode), ShopMessages.BarcodeAlreadyInUse);
        }

        try
        {
            product.AddSku(command.ToDomainDto(), command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await productRepository.UpdateAsync(product, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = command.SkuId };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
