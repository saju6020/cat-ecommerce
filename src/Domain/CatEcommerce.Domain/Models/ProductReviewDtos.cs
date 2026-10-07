using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.11 / section 11.1 C11.</summary>
public sealed record ProductReviewSubmitDto(
    Guid ProductId, Guid SkuId, Guid CustomerId, int Rating, string? Title, string Body,
    DateTimeOffset SubmittedOn, UserContext UserContext, Guid CorrelationId);

public sealed record ProductReviewEditDto(int Rating, string? Title, string Body, UserContext UserContext, Guid CorrelationId);

/// <summary><see cref="Reason"/> is nullable so the aggregate itself can raise
/// <c>ReviewRejectionReasonRequired</c> (section 8.11's own table places this rule on the
/// aggregate, not the validator) when the caller supplied none or an unparseable one.</summary>
public sealed record ProductReviewRejectDto(ReviewRejectionReason? Reason, string? ModerationNote, Guid ModeratedBy, DateTimeOffset ModeratedOn, UserContext UserContext, Guid CorrelationId);
