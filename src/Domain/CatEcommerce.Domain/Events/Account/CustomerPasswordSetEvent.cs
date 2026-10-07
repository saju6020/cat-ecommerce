using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerPasswordSetEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required bool WasFirstPassword { get; init; }
}
