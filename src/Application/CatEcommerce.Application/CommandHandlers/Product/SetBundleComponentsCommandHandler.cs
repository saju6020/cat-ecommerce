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

/// <summary>
/// HO-SHP-001 section 11.4 C13. The handler only gathers facts about the component SKUs' own
/// products (is it a bundle, a prescription product, discontinued) -- the aggregate decides
/// (rule 20). A component SKU that exists nowhere gets no fact, which the aggregate refuses.
/// </summary>
public sealed class SetBundleComponentsCommandHandler(
    ILogger<SetBundleComponentsCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository)
    : ICommandHandlerAsync<SetBundleComponentsCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBundleComponentsCommand command)
    {
        logger.LogInformation("Setting bundle components for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        var facts = new List<BundleComponentFact>();
        foreach (var component in command.Components.DistinctBy(c => c.ComponentSkuId))
        {
            var owner = await productRepository.GetByFilterAsync(p => p.Skus.Any(s => s.Id == component.ComponentSkuId));
            var sku = owner?.Skus.FirstOrDefault(s => s.Id == component.ComponentSkuId);
            if (owner is null || sku is null) continue;

            facts.Add(new BundleComponentFact(
                sku.Id, sku.Code, sku.Status == SkuStatus.Active, owner.Status == ProductStatus.Active,
                owner.Kind == ProductKind.Bundle, owner.PrescriptionRequired));
        }

        var inputs = command.Components.Select(c => new BundleComponentInputDto(Guid.NewGuid(), c.ComponentSkuId, c.Quantity)).ToList();

        try
        {
            product.SetBundleComponents(inputs, facts, command.UserContext, command.CorrelationId);
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
