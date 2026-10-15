namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 -- request bodies. camelCase JSON; money as an amount string; local dates yyyy-MM-dd; times HH:mm.

public sealed class CreateBoardingUnitRequest
{
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string UnitType { get; init; } = default!;
    public int CapacityCats { get; init; }
    public string? Block { get; init; }
    public int SortOrder { get; init; }
}

public sealed class UpdateBoardingUnitRequest
{
    public string Name { get; init; } = default!;
    public string UnitType { get; init; } = default!;
    public int CapacityCats { get; init; }
    public string? Block { get; init; }
    public int SortOrder { get; init; }
}

public sealed class SetBoardingUnitActivationRequest
{
    public bool IsActive { get; init; }
}

public sealed class CreateMaintenanceBlockRequest
{
    public DateOnly StartsOn { get; init; }
    public DateOnly EndsOn { get; init; }
    public string Reason { get; init; } = default!;
}

public sealed class SetBoardingRateCardRequest
{
    public string UnitType { get; init; } = default!;
    public string NightlyRate { get; init; } = default!;
    public string WeeklyRate { get; init; } = default!;
    public string MonthlyRate { get; init; } = default!;
    public string? AdditionalCatNightlyRate { get; init; }
    public Guid TaxClassId { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class RoutineTimeRequest
{
    public string Kind { get; init; } = default!;
    public string Time { get; init; } = default!;
}

public sealed class SetBoardingCareRoutineRequest
{
    public IReadOnlyList<RoutineTimeRequest> Times { get; init; } = [];
}
