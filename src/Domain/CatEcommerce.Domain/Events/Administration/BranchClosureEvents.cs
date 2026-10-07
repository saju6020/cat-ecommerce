using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

/// <summary>HO-ADM-001 §11.5. Also a contract: HO-APT-001 flags appointments inside a new closure.</summary>
public sealed class BranchClosureCreatedEvent : Event
{
    public required Guid BranchId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
    public required ClosureKind Kind { get; init; }
}

public sealed class BranchClosureCancelledEvent : Event
{
    public required Guid BranchId { get; init; }
    public required Guid ClosureId { get; init; }
}
