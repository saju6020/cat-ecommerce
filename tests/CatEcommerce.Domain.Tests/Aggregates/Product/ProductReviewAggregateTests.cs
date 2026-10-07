using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 Phase D -- U-D1, U-D2, U-D3.</summary>
public sealed class ProductReviewAggregateTests
{
    private static ProductReviewSubmitDto SubmitDto(int rating = 5, string? title = "Great", string body = "Loved it.") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rating, title, body, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

    private static ProductReviewAggregate SubmittedReview()
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        review.Submit(SubmitDto());
        return review;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Submit_RatingOutOfRange_Throws(int rating)
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => review.Submit(SubmitDto(rating: rating)));
        Assert.Equal(BusinessViolationCodes.InvalidReviewText, ex.ErrorCode);
    }

    [Fact]
    public void Submit_BodyTooLong_Throws()
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => review.Submit(SubmitDto(body: new string('x', 2001))));
        Assert.Equal(BusinessViolationCodes.InvalidReviewText, ex.ErrorCode);
    }

    [Fact]
    public void Submit_BodyWithNulCharacter_Throws()
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => review.Submit(SubmitDto(body: "Bad\0body")));
        Assert.Equal(BusinessViolationCodes.InvalidReviewText, ex.ErrorCode);
    }

    [Fact]
    public void Submit_BodyWithLineBreaks_Accepted()
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        review.Submit(SubmitDto(body: "Line one\nLine two\r\nLine three"));
        Assert.Equal(ProductReviewStatus.PendingModeration, review.Status);
    }

    [Fact]
    public void StatusTransitions_OnlySpecifiedArrowsAllowed()
    {
        var review = SubmittedReview();
        var moderator = Guid.NewGuid();

        // PendingModeration -> Hide is invalid.
        Assert.Throws<DomainRuleViolationException>(() => review.Hide(moderator, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        review.Publish(moderator, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductReviewStatus.Published, review.Status);

        // Published -> Publish again is invalid.
        Assert.Throws<DomainRuleViolationException>(() => review.Publish(moderator, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        // Published -> Reject is invalid.
        Assert.Throws<DomainRuleViolationException>(() => review.Reject(new ProductReviewRejectDto(ReviewRejectionReason.Spam, null, moderator, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        review.Hide(moderator, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductReviewStatus.Hidden, review.Status);

        // Edit is not allowed from Hidden.
        Assert.Throws<DomainRuleViolationException>(() => review.Edit(new ProductReviewEditDto(4, "Edited", "New body", TestUserContext.Create(), Guid.NewGuid())));

        review.Withdraw(TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(ProductReviewStatus.Withdrawn, review.Status);

        // Withdraw again is invalid.
        Assert.Throws<DomainRuleViolationException>(() => review.Withdraw(TestUserContext.Create(), Guid.NewGuid()));
    }

    [Fact]
    public void Edit_FromPendingPublishedOrRejected_ReturnsToPendingModeration()
    {
        var review = SubmittedReview();
        review.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        review.Edit(new ProductReviewEditDto(3, "Updated", "Updated body", TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(ProductReviewStatus.PendingModeration, review.Status);
        Assert.Equal(3, review.Rating);
        Assert.Null(review.ModeratedOn);
    }

    [Fact]
    public void Reject_NoReason_ThrowsReviewRejectionReasonRequired()
    {
        var review = SubmittedReview();
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            review.Reject(new ProductReviewRejectDto(null, null, Guid.NewGuid(), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.ReviewRejectionReasonRequired, ex.ErrorCode);
    }

    [Fact]
    public void Reject_WithReason_Succeeds()
    {
        var review = SubmittedReview();
        review.Reject(new ProductReviewRejectDto(ReviewRejectionReason.OffTopic, null, Guid.NewGuid(), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(ProductReviewStatus.Rejected, review.Status);
        Assert.Equal(ReviewRejectionReason.OffTopic, review.RejectionReason);
    }
}
