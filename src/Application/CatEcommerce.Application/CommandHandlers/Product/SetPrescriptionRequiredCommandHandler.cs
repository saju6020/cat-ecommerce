using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C8. Changing who may buy a therapeutic food is safety-relevant
/// -- audited by <c>AuditPrescriptionFlagHandler</c> on <c>ProductPrescriptionRequirementChangedEvent</c>.</summary>
public sealed class SetPrescriptionRequiredCommandHandler(
    ILogger<SetPrescriptionRequiredCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IBundleMembershipReader bundleMembership)
    : ICommandHandlerAsync<SetPrescriptionRequiredCommand>
{
    public async Task<CommandResponse> HandleAsync(SetPrescriptionRequiredCommand command)
    {
        logger.LogInformation("Setting prescription flag for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        try
        {
            // OQ-14 / BR-2: only turning the flag ON needs the bundle lookup (the aggregate decides).
            var bundles = command.PrescriptionRequired && !product.PrescriptionRequired
                ? await bundleMembership.FindActiveBundleNamesContainingAsync(product.Skus.Select(s => s.Id).ToList())
                : [];
            product.SetPrescriptionRequired(command.PrescriptionRequired, command.UserContext, command.CorrelationId, bundles);
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
