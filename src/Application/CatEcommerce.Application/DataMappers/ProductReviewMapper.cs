using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-SHP-001 section 11.3.</summary>
public static class ProductReviewMapper
{
    public static ProductReviewSubmitDto ToDomainDto(this SubmitProductReviewCommand command, Guid customerId, DateTimeOffset submittedOn) =>
        new(command.ProductId, command.SkuId, customerId, command.Rating, command.Title, command.Body, submittedOn, command.UserContext, command.CorrelationId);

    public static ProductReviewEditDto ToDomainDto(this EditProductReviewCommand command) =>
        new(command.Rating, command.Title, command.Body, command.UserContext, command.CorrelationId);

    public static ProductReviewRejectDto ToDomainDto(this RejectProductReviewCommand command, ReviewRejectionReason? reason, DateTimeOffset moderatedOn) =>
        new(reason, command.Note, command.UserContext.UserId, moderatedOn, command.UserContext, command.CorrelationId);
}
