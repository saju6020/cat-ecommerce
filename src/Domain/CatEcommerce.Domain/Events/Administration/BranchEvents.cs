using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

/// <summary>HO-ADM-001 §11.5. Ids, dates, enums and booleans only.</summary>
public sealed class BranchCreatedEvent : Event
{
    public required Guid BranchId { get; init; }
}

public sealed class BranchDetailsUpdatedEvent : Event
{
    public required Guid BranchId { get; init; }
}

public sealed class BranchWeeklyHoursChangedEvent : Event
{
    public required Guid BranchId { get; init; }
}

public sealed class BranchActivationChangedEvent : Event
{
    public required Guid BranchId { get; init; }
    public required bool IsActive { get; init; }
}
