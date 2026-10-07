using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Notification;

/// <summary>
/// HO-NTF-001 §11.6 / HO-ACC-001 ADR-041. For each of the customer's notifications: <c>Redact</c>
/// (also nulls the destination); for each request: <c>RedactVariables</c>. Idempotent and
/// resumable -- the loop's own filter excludes rows already anonymised, so it terminates and a
/// retry after a partial failure only touches what is still left. Pending requests are then
/// suppressed by R1 (the recipient directory reports an anonymised customer as inactive).
///
/// Gate fix 2026-09-27 (SECURITY/ARCHITECTURE/CODE_REVIEW HIGH):
/// 1. The loop used to filter on <c>RedactedOn == null</c>, which skips every row already redacted
///    as a <see cref="NotificationRedactionReason.Secret"/> -- every delivered password reset,
///    typically within minutes of being sent. Those rows kept the customer's real mobile/email in
///    <c>Destination</c> forever. Filters on <c>AnonymisedOn == null</c> instead (a separate marker
///    -- see <see cref="NotificationAggregate.Redact"/>), so every row is visited regardless of any
///    prior <see cref="NotificationRedactionReason.Secret"/> redaction.
/// 2. Also processes the merged-away duplicates of this customer (<see cref="INotificationRecipientDirectory.FindMergedDuplicatesAsync"/>):
///    §11.6 keeps an external notification's <c>RecipientId</c> as the pre-merge duplicate, and a
///    merged-away customer can never itself be anonymised (the aggregate requires <c>Active</c>),
///    so those rows would otherwise never be reached.
/// </summary>
public sealed class AnonymiseNotificationsOnCustomerAnonymisedHandler(
    ILogger<AnonymiseNotificationsOnCustomerAnonymisedHandler> logger,
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    INotificationRecipientDirectory recipientDirectory,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        logger.LogInformation("Anonymising notifications for {CustomerId}. CorrelationId: {CorrelationId}", @event.CustomerId, @event.CorrelationId);

        var now = timeProvider.GetUtcNow();

        var duplicateIds = await recipientDirectory.FindMergedDuplicatesAsync(@event.CustomerId);
        var targetIds = new List<Guid> { @event.CustomerId };
        targetIds.AddRange(duplicateIds);

        foreach (var customerId in targetIds)
        {
            NotificationAggregate? notification;
            while ((notification = await notificationRepository.GetByFilterAsync(
                       n => n.RecipientId == customerId && n.AnonymisedOn == null)) is not null)
            {
                notification.Redact(NotificationRedactionReason.Anonymisation, now);
                await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            }

            NotificationRequestAggregate? request;
            while ((request = await requestRepository.GetByFilterAsync(
                       r => r.RecipientId == customerId && r.VariablesRedactedOn == null)) is not null)
            {
                request.RedactVariables(now);
                await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
            }
        }
    }
}
