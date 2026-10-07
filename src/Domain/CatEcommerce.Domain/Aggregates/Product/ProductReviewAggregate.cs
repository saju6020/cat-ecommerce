using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.11 -- own root (bounded-contexts.md rule 3: reviews accumulate without
/// bound). "The SKU must be among the customer's purchased SKUs of this product" and "one
/// non-withdrawn review per customer per product" are handler/repository checks (they span more
/// than this one row); the status machine and its field rules are this aggregate's own.
/// </summary>
public sealed class ProductReviewAggregate : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int Rating { get; private set; }
    public string? Title { get; private set; }
    public string Body { get; private set; } = default!;
    public ProductReviewStatus Status { get; private set; } = default!;
    public DateTimeOffset SubmittedOn { get; private set; }
    public DateTimeOffset? ModeratedOn { get; private set; }
    public Guid? ModeratedBy { get; private set; }
    public ReviewRejectionReason? RejectionReason { get; private set; }
    public string? ModerationNote { get; private set; }

    private ProductReviewAggregate() { }

    public ProductReviewAggregate(Guid id) : base(id) { }

    public void Submit(ProductReviewSubmitDto dto)
    {
        ValidateRating(dto.Rating);
        ValidateTitle(dto.Title);
        ValidateBody(dto.Body);

        ProductId = dto.ProductId;
        SkuId = dto.SkuId;
        CustomerId = dto.CustomerId;
        Rating = dto.Rating;
        Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        Body = dto.Body;
        Status = ProductReviewStatus.PendingModeration;
        SubmittedOn = dto.SubmittedOn;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ProductReviewSubmittedEvent { ReviewId = Id, ProductId = ProductId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Edit (Published | Rejected | PendingModeration) -&gt; PendingModeration.</summary>
    public void Edit(ProductReviewEditDto dto)
    {
        if (Status == ProductReviewStatus.Withdrawn || Status == ProductReviewStatus.Hidden)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewTransition,
                $"Cannot edit a review from {Status.DisplayName}.", nameof(Status));

        ValidateRating(dto.Rating);
        ValidateTitle(dto.Title);
        ValidateBody(dto.Body);

        Rating = dto.Rating;
        Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        Body = dto.Body;
        Status = ProductReviewStatus.PendingModeration;
        ModeratedOn = null;
        ModeratedBy = null;
        RejectionReason = null;
        ModerationNote = null; // row 36: a note about the previous text must not survive the edit

        UpdateVersion();
    }

    /// <summary>Row 21 / ADR-096: anonymisation keeps only a PUBLISHED review (its author reads "A verified
    /// buyer"). Every other status -- pending, rejected, hidden, withdrawn -- is never shown to the public,
    /// so its free text (and the moderator's note about it) is cleared. A review still awaiting moderation is
    /// also taken out of the queue (Withdrawn): with its text gone a moderator must not be able to publish it
    /// (BSB1). Idempotent; returns whether anything changed.</summary>
    public bool ClearTextForAnonymisation()
    {
        if (Status == ProductReviewStatus.Published) return false;
        if (Title is null && Body.Length == 0 && ModerationNote is null) return false;

        Title = null;
        Body = "";
        ModerationNote = null;
        if (Status == ProductReviewStatus.PendingModeration) Status = ProductReviewStatus.Withdrawn;
        UpdateVersion();
        return true;
    }

    public void Withdraw(Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status == ProductReviewStatus.Withdrawn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewTransition, "This review is already withdrawn.", nameof(Status));

        Status = ProductReviewStatus.Withdrawn;

        AddDomainEvent(new ProductReviewWithdrawnEvent { ReviewId = Id, ProductId = ProductId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void Publish(Guid moderatedBy, DateTimeOffset moderatedOn, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status != ProductReviewStatus.PendingModeration)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewTransition, $"Cannot publish from {Status.DisplayName}.", nameof(Status));

        Status = ProductReviewStatus.Published;
        ModeratedBy = moderatedBy;
        ModeratedOn = moderatedOn;
        RejectionReason = null;

        AddDomainEvent(new ProductReviewPublishedEvent { ReviewId = Id, ProductId = ProductId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void Reject(ProductReviewRejectDto dto)
    {
        if (Status != ProductReviewStatus.PendingModeration)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewTransition, $"Cannot reject from {Status.DisplayName}.", nameof(Status));

        if (dto.Reason is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReviewRejectionReasonRequired, "A rejection reason is required.", nameof(RejectionReason));

        if (dto.ModerationNote is { Length: > ReviewRules.ModerationNoteMaxLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Moderation note cannot exceed 500 characters.", nameof(ModerationNote));

        Status = ProductReviewStatus.Rejected;
        ModeratedBy = dto.ModeratedBy;
        ModeratedOn = dto.ModeratedOn;
        RejectionReason = dto.Reason;
        ModerationNote = string.IsNullOrWhiteSpace(dto.ModerationNote) ? null : dto.ModerationNote.Trim();

        AddDomainEvent(new ProductReviewRejectedEvent { ReviewId = Id, ProductId = ProductId, Reason = dto.Reason.Value, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Hide(Guid moderatedBy, DateTimeOffset moderatedOn, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status != ProductReviewStatus.Published)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewTransition, $"Cannot hide from {Status.DisplayName}.", nameof(Status));

        Status = ProductReviewStatus.Hidden;
        ModeratedBy = moderatedBy;
        ModeratedOn = moderatedOn;

        AddDomainEvent(new ProductReviewHiddenEvent { ReviewId = Id, ProductId = ProductId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>Merge (ADR-096): the review moves to the surviving customer. The handler withdraws
    /// the duplicate's review instead when the survivor already reviewed the product (the filtered
    /// unique index allows one non-withdrawn review per customer per product). Idempotent.</summary>
    public bool ReassignCustomer(Guid survivingCustomerId)
    {
        if (CustomerId == survivingCustomerId) return false;

        CustomerId = survivingCustomerId;
        UpdateVersion();
        return true;
    }

    private static void ValidateRating(int rating)
    {
        if (rating is < ReviewRules.MinRating or > ReviewRules.MaxRating)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Rating must be between 1 and 5.", nameof(Rating));
    }

    private static void ValidateTitle(string? title)
    {
        if (title is { Length: > ReviewRules.TitleMaxLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Title cannot exceed 120 characters.", nameof(Title));

        // Row 36: plain text, like the body -- control characters other than line breaks are refused.
        if (ControlCharacters.ContainsDisallowed(title))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Title contains a disallowed character.", nameof(Title));
    }

    private static void ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > ReviewRules.BodyMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Review body must be 1-2000 characters.", nameof(Body));

        // Plain text: control characters other than line breaks refused.
        if (ControlCharacters.ContainsDisallowed(body))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReviewText, "Review body contains a disallowed character.", nameof(Body));
    }
}
