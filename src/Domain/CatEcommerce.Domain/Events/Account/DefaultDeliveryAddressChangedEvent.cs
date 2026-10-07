using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class DefaultDeliveryAddressChangedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required Guid AddressId { get; init; }
}
