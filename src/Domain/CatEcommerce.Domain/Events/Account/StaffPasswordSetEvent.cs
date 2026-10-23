using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffPasswordSetEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required bool WasFirstPassword { get; init; }
    /// <summary>Owner, 2026-10-19: the administrator who set it, when it was not the staff member themselves.</summary>
    public Guid? SetByAdministratorId { get; init; }
}
