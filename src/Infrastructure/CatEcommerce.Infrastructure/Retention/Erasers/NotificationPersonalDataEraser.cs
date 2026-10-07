using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Retention.Erasers;

/// <summary>
/// HO-XCT-001 §5.2 C-ALL-2 / §8.8 -- gate fix 2026-09-27 (ARCHITECTURE HIGH). The Notification
/// context stores a customer's personal data beyond the id: the destination (mobile/email),
/// rendered bodies carrying the recipient's name, and a request's own variables. Reuses the exact
/// same domain operations the anonymisation event handler uses
/// (<see cref="NotificationAggregate.Redact"/> with <see cref="NotificationRedactionReason.Anonymisation"/>,
/// <see cref="NotificationRequestAggregate.RedactVariables"/>), and covers the same merged-away
/// duplicates via <see cref="INotificationRecipientDirectory.FindMergedDuplicatesAsync"/> -- so the
/// event-driven anonymisation path and this retention-worker-driven path can never disagree on what
/// "erased" means for this context.
///
/// No table this context owns is <c>Clinical</c>/<c>Financial</c>/<c>Regulatory</c>
/// (<c>RecordClassification.Notifications.cs</c>), so nothing is ever legally held -- this eraser
/// always returns <see cref="ErasureOutcome.Completed"/>.
/// </summary>
public sealed class NotificationPersonalDataEraser(
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    INotificationRecipientDirectory recipientDirectory,
    TimeProvider timeProvider)
    : IPersonalDataEraser
{
    public string ContextKey => "notification";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        var duplicateIds = await recipientDirectory.FindMergedDuplicatesAsync(customerAccountId, ct);
        var targetIds = new List<Guid> { customerAccountId };
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

        return new ErasureOutcome.Completed();
    }
}
