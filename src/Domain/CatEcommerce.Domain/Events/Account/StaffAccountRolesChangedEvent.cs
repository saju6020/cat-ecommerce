using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffAccountRolesChangedEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required IReadOnlyList<string> PreviousRoleIds { get; init; }
    public required IReadOnlyList<string> NewRoleIds { get; init; }
}
