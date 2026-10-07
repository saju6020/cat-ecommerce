using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

/// <summary>Ids only (§8.10a) -- never a name, identifier or any other personal field.</summary>
public sealed class CustomerAccountAnonymisedEvent : Event
{
    public required Guid CustomerId { get; init; }
}
