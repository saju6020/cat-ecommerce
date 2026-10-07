using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Administration;
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
/// HO-SHP-001 section 11.4 C7. Builds <see cref="PublicationFacts"/>: the ids of active SKUs priced
/// now (a per-SKU existence check is enough -- "priced now" only needs one row with
/// <c>EffectiveFrom &lt;= now</c> to exist, section 8.5), the tax class's <c>IsActive</c>, and (once
/// HO-SHP-001 Phase F lands <c>SetBundleComponents</c>) each component's facts.
/// </summary>
public sealed class PublishProductCommandHandler(
    ILogger<PublishProductCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<SkuPriceAggregate> priceRepository,
    IAggregateRootRepository<TaxClassAggregate> taxClassRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PublishProductCommand>
{
    public async Task<CommandResponse> HandleAsync(PublishProductCommand command)
    {
        logger.LogInformation("Publishing product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        var now = timeProvider.GetUtcNow();
        var pricedSkuIds = new List<Guid>();
        foreach (var sku in product.Skus.Where(s => s.Status == SkuStatus.Active))
        {
            if (await priceRepository.ExistAsync(p => p.SkuId == sku.Id && p.EffectiveFrom <= now))
                pricedSkuIds.Add(sku.Id);
        }

        var taxClass = await taxClassRepository.GetByIdAsync(product.TaxClassId);
        var bundleComponentFacts = new List<BundleComponentFact>();

        foreach (var component in product.BundleComponents)
        {
            var owner = await productRepository.GetByFilterAsync(p => p.Skus.Any(s => s.Id == component.ComponentSkuId));
            var sku = owner?.Skus.FirstOrDefault(s => s.Id == component.ComponentSkuId);
            bundleComponentFacts.Add(new BundleComponentFact(
                component.ComponentSkuId,
                sku?.Code ?? component.ComponentSkuId.ToString(),
                sku?.Status == SkuStatus.Active,
                owner?.Status == ProductStatus.Active,
                owner?.Kind == ProductKind.Bundle,
                owner?.PrescriptionRequired ?? false));
        }

        var facts = new PublicationFacts(pricedSkuIds, taxClass?.IsActive ?? false, bundleComponentFacts);

        try
        {
            product.Publish(facts, now, command.UserContext, command.CorrelationId);
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
