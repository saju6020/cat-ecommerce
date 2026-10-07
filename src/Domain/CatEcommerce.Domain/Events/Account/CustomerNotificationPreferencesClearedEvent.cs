using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerNotificationPreferencesClearedEvent : Event
{
    public required Guid CustomerId { get; init; }
}
