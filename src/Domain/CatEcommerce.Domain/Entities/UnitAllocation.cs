using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-BRD-001 section 8.3/8.4 (ADR-219, renumbered ADR-352) -- the unit a stay holds for an instant range. Owned by the stay (at most ten: a long stay may be
/// relocated). <see cref="IsActive"/> is denormalised from the stay's status and maintained by the aggregate in every transition, because the exclusion
/// constraint <c>ex_stay_allocation_unit</c> can read only this row's own columns (F31).
/// </summary>
public sealed class UnitAllocation
{
    public Guid Id { get; private set; }
    public Guid UnitId { get; private set; }
    public BoardingUnitType UnitType { get; private set; } = default!;
    public DateTimeOffset OccupiedFrom { get; private set; }
    public DateTimeOffset OccupiedTo { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Why this allocation exists after the first: <c>Relocated</c> reason text (section 8.15) -- null for the original.</summary>
    public string? Reason { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }

    private UnitAllocation() { }

    public UnitAllocation(Guid id, Guid unitId, BoardingUnitType unitType, DateTimeOffset from, DateTimeOffset to, bool isActive, string? reason, Guid createdBy, DateTimeOffset createdOn)
    {
        Id = id;
        UnitId = unitId;
        UnitType = unitType;
        OccupiedFrom = from;
        OccupiedTo = to;
        IsActive = isActive;
        Reason = reason;
        CreatedBy = createdBy;
        CreatedOn = createdOn;
    }

    internal void SetRange(DateTimeOffset from, DateTimeOffset to)
    {
        OccupiedFrom = from;
        OccupiedTo = to;
    }

    internal void SetActive(bool isActive) => IsActive = isActive;
}
