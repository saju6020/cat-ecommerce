using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class StaffBranchesAssignedEvent : Event
{
    public required Guid StaffAccountId { get; init; }
}
