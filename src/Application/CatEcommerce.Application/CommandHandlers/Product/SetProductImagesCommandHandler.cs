using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>
/// HO-SHP-001 section 11.4 C5 / HO-FIL-001 (ADR-318, closes README C-5). <c>SetImages</c> replaces
/// the whole set at once (section 8.3.2), so only a FileId that was not already attached to this
/// product needs to pass <see cref="StoredFileAggregate.IsEligibleForAttach"/> -- re-submitting an
/// already-attached image (carried over unchanged from a previous save) must not fail merely
/// because HO-FIL-001's 30-minute attach window has since elapsed (the same precedent
/// <c>AddCatPhotoCommandHandler</c> sets, adapted for a wholesale-replace command instead of an
/// incremental add).
/// </summary>
public sealed class SetProductImagesCommandHandler(
    ILogger<SetProductImagesCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetProductImagesCommand>
{
    public async Task<CommandResponse> HandleAsync(SetProductImagesCommand command)
    {
        logger.LogInformation("Setting images for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.ProductVersionConflict, nameof(command.ExpectedVersion), $"Expected version {command.ExpectedVersion} but the product is at {product.Version}.");

        var existingFileIds = product.Images.Select(i => i.FileId).ToHashSet();
        var now = timeProvider.GetUtcNow();

        foreach (var image in command.Images)
        {
            if (existingFileIds.Contains(image.FileId)) continue;

            var file = await fileRepository.GetByIdAsync(image.FileId);
            if (file is null || !file.IsEligibleForAttach(FilePurpose.ProductImage, command.UserContext.UserId, now, FileRules.AttachWindow))
                return Violation(BusinessViolationCodes.InvalidImage, nameof(image.FileId), "An uploaded image could not be attached. Upload it again and try immediately.");
        }

        try
        {
            product.SetImages(command.ToDomainDto(), command.UserContext, command.CorrelationId);
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
