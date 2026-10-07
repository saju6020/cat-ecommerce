using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Notification;

/// <summary>
/// HO-NTF-001 §11.6 / HO-ACC-001 ADR-039. For each <c>InApp</c> notification of the duplicate:
/// reassign to the survivor, one <c>UpdateAsync</c> each. Idempotent and resumable, same idiom as
/// <c>ReassignCatsOnCustomerMergedHandler</c> -- the loop terminates when none still match the
/// duplicate, which is also true after a partial failure is retried by re-sending the merge
/// command. The external log keeps the original recipient; only the inbox copy moves.
/// </summary>
public sealed class ReassignInboxOnCustomerMergedHandler(
    ILogger<ReassignInboxOnCustomerMergedHandler> logger,
    IAggregateRootRepository<NotificationAggregate> notificationRepository)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        logger.LogInformation(
            "Reassigning inbox items from {DuplicateCustomerId} to {SurvivingCustomerId}. CorrelationId: {CorrelationId}",
            @event.DuplicateCustomerId, @event.SurvivingCustomerId, @event.CorrelationId);

        NotificationAggregate? notification;
        while ((notification = await notificationRepository.GetByFilterAsync(
                   n => n.RecipientId == @event.DuplicateCustomerId && n.Channel == NotificationChannel.InApp)) is not null)
        {
            notification.ReassignRecipientOnMerge(@event.SurvivingCustomerId);
            await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
