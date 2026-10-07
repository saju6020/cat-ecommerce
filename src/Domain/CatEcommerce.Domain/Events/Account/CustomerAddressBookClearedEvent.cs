using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerAddressBookClearedEvent : Event
{
    public required Guid CustomerId { get; init; }
}
