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

/// <summary>HO-SHP-001 section 11.4 C11 -- own review only (ReviewNotFound -&gt; 404, never
/// distinguishing "not yours" from "does not exist").</summary>
public sealed class EditProductReviewCommandHandler(
    ILogger<EditProductReviewCommandHandler> logger,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository)
    : ICommandHandlerAsync<EditProductReviewCommand>
{
    public async Task<CommandResponse> HandleAsync(EditProductReviewCommand command)
    {
        logger.LogInformation("Editing review {ReviewId}. CorrelationId: {CorrelationId}", command.ReviewId, command.CorrelationId);

        var review = await reviewRepository.GetByIdAsync(command.ReviewId);
        if (review is null || review.CustomerId != command.UserContext.UserId)
            return Violation(BusinessViolationCodes.ReviewNotFound, nameof(command.ReviewId), $"Review '{command.ReviewId}' was not found.");

        try
        {
            review.Edit(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await reviewRepository.UpdateAsync(review, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductReviewAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
