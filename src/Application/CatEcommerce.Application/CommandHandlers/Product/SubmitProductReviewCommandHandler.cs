using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C11 -- Product Active; SKU belongs to it;
/// <c>IPurchaseHistoryReader</c> says the customer bought that SKU; AlreadyReviewed pre-check
/// (backed by the filtered unique index, section 12.5).</summary>
public sealed class SubmitProductReviewCommandHandler(
    ILogger<SubmitProductReviewCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository,
    IPurchaseHistoryReader purchaseHistoryReader,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SubmitProductReviewCommand>
{
    public async Task<CommandResponse> HandleAsync(SubmitProductReviewCommand command)
    {
        logger.LogInformation("Submitting review for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null || product.Status != ProductStatus.Active)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        if (product.Skus.All(s => s.Id != command.SkuId))
            return Violation(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), $"SKU '{command.SkuId}' does not belong to this product.");

        var customerId = command.UserContext.UserId;
        var purchasedSkuIds = await purchaseHistoryReader.GetPurchasedSkuIdsAsync(customerId, [command.SkuId], CancellationToken.None);
        if (!purchasedSkuIds.Contains(command.SkuId))
            return Violation(BusinessViolationCodes.ReviewNotPermitted, nameof(command.SkuId), "Only customers who bought this product can review it.");

        if (await reviewRepository.ExistAsync(r => r.ProductId == command.ProductId && r.CustomerId == customerId && r.Status != ProductReviewStatus.Withdrawn))
            return Violation(BusinessViolationCodes.AlreadyReviewed, nameof(command.ProductId), ShopMessages.AlreadyReviewed);

        var review = new ProductReviewAggregate(command.ReviewId);

        try
        {
            review.Submit(command.ToDomainDto(customerId, timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await reviewRepository.SaveAsync(review, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = review.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductReviewAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
