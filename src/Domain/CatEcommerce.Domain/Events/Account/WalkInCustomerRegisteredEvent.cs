using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class WalkInCustomerRegisteredEvent : Event
{
    public required Guid CustomerId { get; init; }
}
