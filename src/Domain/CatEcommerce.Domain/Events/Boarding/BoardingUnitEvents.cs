using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Boarding;

// HO-BRD-001 section 11.5. Ids, instants, dates, enums, amounts and booleans only -- no names, notes or descriptions (U-X1).

public sealed class BoardingUnitCreatedEvent : Event
{
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
    public required BoardingUnitType Type { get; init; }
}

public sealed class BoardingUnitUpdatedEvent : Event
{
    public required Guid UnitId { get; init; }
    public required BoardingUnitType Type { get; init; }
}

public sealed class BoardingUnitActivationChangedEvent : Event
{
    public required Guid UnitId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class UnitMaintenanceBlockCreatedEvent : Event
{
    public required Guid BlockId { get; init; }
    public required Guid UnitId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
}

public sealed class UnitMaintenanceBlockCancelledEvent : Event
{
    public required Guid BlockId { get; init; }
    public required Guid UnitId { get; init; }
}

public sealed class BoardingRateCardSetEvent : Event
{
    public required Guid RateCardId { get; init; }
    public required BoardingUnitType UnitType { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class BoardingCareRoutineSetEvent : Event
{
    public required Guid BranchId { get; init; }
}
