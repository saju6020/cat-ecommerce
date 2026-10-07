using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffSessionIssuedEvent : Event
{
    public required Guid SessionId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required DateTimeOffset ExpiresOn { get; init; }
}
