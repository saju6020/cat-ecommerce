using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffAccountCreatedEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required IReadOnlyList<string> RoleIds { get; init; }
}
