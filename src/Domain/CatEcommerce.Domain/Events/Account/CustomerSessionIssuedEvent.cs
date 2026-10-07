using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerSessionIssuedEvent : Event
{
    public required Guid SessionId { get; init; }
    public required Guid CustomerAccountId { get; init; }
    public required DateTimeOffset ExpiresOn { get; init; }
}
