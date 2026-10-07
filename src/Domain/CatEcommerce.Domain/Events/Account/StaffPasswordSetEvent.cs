using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffPasswordSetEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required bool WasFirstPassword { get; init; }
}
