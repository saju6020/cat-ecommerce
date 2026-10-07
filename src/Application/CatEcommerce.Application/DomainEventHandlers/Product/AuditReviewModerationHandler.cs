using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Product;

/// <summary>HO-SHP-001 section 11.6 -- a BusinessEvent row with the moderator, the reason and the
/// note. The note lives on the aggregate, not on the event (section 11.5's "no notes, reasons"
/// rule), so it is read back here. Never enqueues (HO-NTF-001 ADR-057).</summary>
public sealed class AuditReviewModerationHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<ProductReviewPublishedEvent>, IEventHandlerAsync<ProductReviewRejectedEvent>, IEventHandlerAsync<ProductReviewHiddenEvent>
{
    public Task HandleAsync(ProductReviewPublishedEvent @event) => RecordAsync(@event.ReviewId, "ReviewPublished", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(ProductReviewRejectedEvent @event) => RecordAsync(@event.ReviewId, "ReviewRejected", @event.UserContext, @event.CorrelationId);

    public Task HandleAsync(ProductReviewHiddenEvent @event) => RecordAsync(@event.ReviewId, "ReviewHidden", @event.UserContext, @event.CorrelationId);

    private async Task RecordAsync(Guid reviewId, string action, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        var review = await reviewRepository.GetByIdAsync(reviewId);

        var afterValue = review is null ? null : $"status={review.Status.DisplayName};reason={review.RejectionReason?.DisplayName};note={review.ModerationNote}";

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "ProductReview", EntityId: reviewId, Action: action, Reason: action,
            ActorUserId: userContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: afterValue,
            UserContext: userContext, CorrelationId: correlationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
