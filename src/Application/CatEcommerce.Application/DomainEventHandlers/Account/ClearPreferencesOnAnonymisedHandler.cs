using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>HO-ACC-001 §11.7 / §8.10a.</summary>
public sealed class ClearPreferencesOnAnonymisedHandler(
    ILogger<ClearPreferencesOnAnonymisedHandler> logger,
    IAggregateRootRepository<CustomerNotificationPreferencesAggregate> preferencesRepository)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        var preferences = await preferencesRepository.GetByIdAsync(@event.CustomerId);
        if (preferences is null) return;

        logger.LogInformation("Clearing notification preferences for anonymised customer {CustomerId}. CorrelationId: {CorrelationId}", @event.CustomerId, @event.CorrelationId);

        preferences.Clear(new CustomerNotificationPreferencesClearDto(@event.UserContext, @event.CorrelationId));
        await preferencesRepository.UpdateAsync(preferences, EventPublishOption.InMemoryPublishOnly);
    }
}
