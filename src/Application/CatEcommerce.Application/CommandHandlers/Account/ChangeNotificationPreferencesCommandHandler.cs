using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C8) / US-ACC-06. Lazily creates the preferences root keyed by the
/// customer id (ADR-035) on its first write.</summary>
public sealed class ChangeNotificationPreferencesCommandHandler(
    ILogger<ChangeNotificationPreferencesCommandHandler> logger,
    IAggregateRootRepository<CustomerNotificationPreferencesAggregate> preferencesRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ChangeNotificationPreferencesCommand>
{
    private const string Source = "CustomerPortal";

    public async Task<CommandResponse> HandleAsync(ChangeNotificationPreferencesCommand command)
    {
        logger.LogInformation("Changing notification preferences. CorrelationId: {CorrelationId}", command.CorrelationId);

        List<NotificationPreferenceChangeDto> changes;
        try
        {
            changes = command.Changes
                .Select(c => new NotificationPreferenceChangeDto(
                    Enumeration.FromDisplayName<NotificationChannel>(c.Channel),
                    Enumeration.FromDisplayName<NotificationCategory>(c.Category),
                    c.IsGranted))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Violation(BusinessViolationCodes.NotificationPreferenceNotEditable, "Changes", "One or more channel/category values were not recognised.");
        }

        // HO-NTF-001 §8.5/§11.4 (amended C8): a channel the account has no identifier for is refused.
        NotificationChannel? preferredChannel = null;
        if (!string.IsNullOrEmpty(command.PreferredChannel))
        {
            if (!EnumerationParsing.TryParse<NotificationChannel>(command.PreferredChannel, out var parsedChannel) ||
                parsedChannel is null || (parsedChannel != NotificationChannel.Sms && parsedChannel != NotificationChannel.Email))
                return Violation(BusinessViolationCodes.InvalidPreferredChannel, "PreferredChannel", "The preferred channel must be Sms or Email.");

            var customer = await customerRepository.GetByIdAsync(command.UserContext.UserId);
            if (customer is null) return Violation(BusinessViolationCodes.InvalidPreferredChannel, "PreferredChannel", "Account not found.");

            var hasDestination = parsedChannel == NotificationChannel.Sms ? customer.MobileNumber is not null : customer.EmailAddress is not null;
            if (!hasDestination)
                return Violation(BusinessViolationCodes.PreferredChannelHasNoDestination, "PreferredChannel", "The account has no identifier for that channel.");

            preferredChannel = parsedChannel;
        }

        var customerId = command.UserContext.UserId;
        var preferences = await preferencesRepository.GetByIdAsync(customerId);
        var isNew = preferences is null;

        if (isNew)
        {
            preferences = new CustomerNotificationPreferencesAggregate(customerId);
            preferences.Open(new CustomerNotificationPreferencesOpenDto(command.UserContext, command.CorrelationId));
        }

        try
        {
            if (changes.Count > 0)
                preferences!.ChangePreferences(command.ToChangeDto(changes, timeProvider.GetUtcNow(), Source));

            if (preferredChannel is not null)
                preferences!.ChangePreferredChannel(preferredChannel, command.CorrelationId, command.UserContext);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew)
            await preferencesRepository.SaveAsync(preferences!, EventPublishOption.InMemoryPublishOnly);
        else
            await preferencesRepository.UpdateAsync(preferences!, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(CustomerNotificationPreferencesAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
