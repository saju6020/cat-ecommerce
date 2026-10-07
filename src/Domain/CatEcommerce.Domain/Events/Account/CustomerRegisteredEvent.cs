using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerRegisteredEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required bool HasMobile { get; init; }
    public required bool HasEmail { get; init; }
}
