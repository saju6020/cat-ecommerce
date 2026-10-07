using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §11.4 C8 -- another customer's item is <c>NotificationNotFound</c> (404),
/// never a 403 (never disclosing that the id belongs to someone else).</summary>
public sealed class MarkInboxItemReadCommandHandler(
    IAggregateRootRepository<NotificationAggregate> notificationRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<MarkInboxItemReadCommand>
{
    public async Task<CommandResponse> HandleAsync(MarkInboxItemReadCommand command)
    {
        var notification = await notificationRepository.GetByIdAsync(command.NotificationId);
        if (notification is null || notification.Channel != NotificationChannel.InApp || notification.RecipientId != command.UserContext.UserId)
            return Violation(BusinessViolationCodes.NotificationNotFound, nameof(command.NotificationId), "Notification not found.");

        notification.MarkRead(timeProvider.GetUtcNow());
        await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(NotificationAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
