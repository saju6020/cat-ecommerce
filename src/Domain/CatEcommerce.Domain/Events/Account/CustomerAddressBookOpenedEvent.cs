using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerAddressBookOpenedEvent : Event
{
    public required Guid CustomerId { get; init; }
}
