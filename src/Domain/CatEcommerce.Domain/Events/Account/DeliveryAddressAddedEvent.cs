using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class DeliveryAddressAddedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required Guid AddressId { get; init; }
    public required bool BecameDefault { get; init; }
}
