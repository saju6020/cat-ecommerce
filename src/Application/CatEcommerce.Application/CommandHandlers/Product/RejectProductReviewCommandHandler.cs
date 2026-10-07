using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C12.</summary>
public sealed class RejectProductReviewCommandHandler(
    ILogger<RejectProductReviewCommandHandler> logger,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RejectProductReviewCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectProductReviewCommand command)
    {
        logger.LogInformation("Rejecting review {ReviewId}. CorrelationId: {CorrelationId}", command.ReviewId, command.CorrelationId);

        var review = await reviewRepository.GetByIdAsync(command.ReviewId);
        if (review is null)
            return Violation(BusinessViolationCodes.ReviewNotFound, nameof(command.ReviewId), $"Review '{command.ReviewId}' was not found.");

        // A blank reason is not a parse failure -- EnumerationParsing.TryParse treats it as "no
        // value" and returns true with a null result. Only a genuinely unrecognised, non-blank
        // string is a handler-level 400; a blank one flows through to the aggregate, which raises
        // ReviewRejectionReasonRequired (section 8.11's own table places this rule there).
        ReviewRejectionReason? reason = null;
        if (!string.IsNullOrWhiteSpace(command.Reason))
        {
            if (!EnumerationParsing.TryParse<ReviewRejectionReason>(command.Reason, out reason) || reason is null)
                return Violation(BusinessViolationCodes.InvalidReviewText, nameof(command.Reason), $"'{command.Reason}' is not a known rejection reason.");
        }

        try
        {
            review.Reject(command.ToDomainDto(reason, timeProvider.GetUtcNow()));
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
