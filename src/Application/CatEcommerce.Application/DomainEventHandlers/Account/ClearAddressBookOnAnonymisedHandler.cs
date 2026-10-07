using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>HO-ACC-001 §11.7 / §8.10a.</summary>
public sealed class ClearAddressBookOnAnonymisedHandler(
    ILogger<ClearAddressBookOnAnonymisedHandler> logger,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBookRepository)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        var addressBook = await addressBookRepository.GetByIdAsync(@event.CustomerId);
        if (addressBook is null) return;

        logger.LogInformation("Clearing address book for anonymised customer {CustomerId}. CorrelationId: {CorrelationId}", @event.CustomerId, @event.CorrelationId);

        addressBook.Clear(new CustomerAddressBookClearDto(@event.UserContext, @event.CorrelationId));
        await addressBookRepository.UpdateAsync(addressBook, EventPublishOption.InMemoryPublishOnly);
    }
}
