using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BRD-001 section 8.2 -- one scheduled routine task time of a branch (litter change, enrichment, observation check). Owned by
/// <see cref="Aggregates.Boarding.BoardingCareRoutineAggregate"/>; the list is replaced wholesale by <c>SetTimes</c> (current state).</summary>
public sealed class RoutineTime
{
    public Guid Id { get; private set; }
    public RoutineTaskKind Kind { get; private set; } = default!;
    public TimeOnly LocalTime { get; private set; }

    private RoutineTime() { }

    public RoutineTime(Guid id, RoutineTaskKind kind, TimeOnly localTime)
    {
        Id = id;
        Kind = kind;
        LocalTime = localTime;
    }
}
