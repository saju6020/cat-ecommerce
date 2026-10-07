using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §11.4 C6 / ADR-063 -- idempotent; sent by the worker's redaction sweep.</summary>
public sealed class RedactNotificationSecretsCommandHandler(
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RedactNotificationSecretsCommand>
{
    public async Task<CommandResponse> HandleAsync(RedactNotificationSecretsCommand command)
    {
        var now = timeProvider.GetUtcNow();

        if (command.RequestId is { } requestId)
        {
            var request = await requestRepository.GetByIdAsync(requestId);
            if (request is not null && request.ContainsSecret && request.NotAfter is not null && request.NotAfter < now)
            {
                request.RedactVariables(now);
                await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
            }
        }

        if (command.NotificationId is { } notificationId)
        {
            var notification = await notificationRepository.GetByIdAsync(notificationId);
            if (notification is not null && notification.ContainsSecret && notification.IsTerminal)
            {
                notification.Redact(NotificationRedactionReason.Secret, now);
                await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            }
        }

        return new CommandResponse { IsSuccess = true };
    }
}
