using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>
/// HO-NTF-001 §8.11/§11.4 C9 -- the caller is anonymous (a public endpoint), so <c>UserContext</c>
/// is the system actor; the customer id is the audited entity's own id regardless (the
/// <c>CustomerNotificationPreferencesAggregate</c> is keyed by the customer id, ADR-035).
///
/// Gate fix 2026-09-27 (CODE_REVIEW MEDIUM). <c>notification.RecipientId</c> is deliberately the
/// PRE-MERGE customer for an external notification (§11.6, "it is history") -- but
/// <c>CustomerNotificationPreferencesAggregate</c> is keyed by the CURRENT customer id, so reading
/// or writing it by the notification's raw <c>RecipientId</c> targets the pre-merge duplicate, not
/// the survivor a merged customer's preferences actually live on. Resolves through the same
/// merged-to-survivor walk <see cref="INotificationRecipientDirectory"/> already does for routing,
/// so an old marketing email's unsubscribe link revokes consent on the account that is actually
/// still receiving mail, not a dead duplicate.
/// </summary>
public sealed class UnsubscribeFromMarketingCommandHandler(
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    IAggregateRootRepository<CustomerNotificationPreferencesAggregate> preferencesRepository,
    INotificationRecipientDirectory recipientDirectory,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UnsubscribeFromMarketingCommand>
{
    private const string Source = "unsubscribe-link";

    public async Task<CommandResponse> HandleAsync(UnsubscribeFromMarketingCommand command)
    {
        var hash = Hash(command.Token);
        var notification = await notificationRepository.GetByFilterAsync(n => n.UnsubscribeTokenHash == hash);
        if (notification is null)
            return Violation(BusinessViolationCodes.InvalidUnsubscribeLink, nameof(command.Token), "This unsubscribe link is not valid.");

        var recipient = await recipientDirectory.ResolveAsync(notification.RecipientKind, notification.RecipientId);
        var currentRecipientId = recipient?.Id ?? notification.RecipientId;

        var preferences = await preferencesRepository.GetByIdAsync(currentRecipientId);
        var wasSubscribed = preferences?.MarketingConsents.FirstOrDefault(c => c.Channel == notification.Channel)?.IsGranted
            ?? NotificationPreferenceRules.DefaultMarketingGranted;

        if (wasSubscribed)
        {
            var isNew = preferences is null;
            var systemContext = new Platform.Infrastructure.Common.Security.UserContext(SystemActors.NotificationWorker, Guid.Empty);

            if (isNew)
            {
                preferences = new CustomerNotificationPreferencesAggregate(currentRecipientId);
                preferences.Open(new CustomerNotificationPreferencesOpenDto(systemContext, command.CorrelationId));
            }

            preferences!.ChangePreferences(new ChangeNotificationPreferencesDto(
                [new NotificationPreferenceChangeDto(notification.Channel, NotificationCategory.Marketing, false)],
                Source, timeProvider.GetUtcNow(), systemContext, command.CorrelationId));

            if (isNew)
                await preferencesRepository.SaveAsync(preferences, EventPublishOption.InMemoryPublishOnly);
            else
                await preferencesRepository.UpdateAsync(preferences, EventPublishOption.InMemoryPublishOnly);
        }

        return new CommandResponse { IsSuccess = true, Result = new UnsubscribeOutcome(notification.Channel.DisplayName, wasSubscribed) };
    }

    private static string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(NotificationAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
