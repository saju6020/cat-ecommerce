using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

/// <summary>HO-BRD-001 section 12.6 (Phase B). Projected, untracked reads; nothing here writes.</summary>
public sealed class GetBoardingUnitsQuery : Query<IReadOnlyList<BoardingUnitViewModel>>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetUnitMaintenanceQuery : Query<IReadOnlyList<MaintenanceBlockViewModel>>
{
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetBoardingRateCardsQuery : Query<IReadOnlyList<BoardingRateCardViewModel>>
{
    public required Guid BranchId { get; init; }

    /// <summary>A unit type display name; <c>null</c> lists every type.</summary>
    public string? UnitType { get; init; }
}

public sealed class GetBoardingCareRoutineQuery : Query<BoardingCareRoutineViewModel>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetBoardingUnitTypesQuery : Query<IReadOnlyList<BoardingUnitTypeViewModel>>
{
    /// <summary>The branch (the seeded main branch when omitted). When given, capacity is the largest of the branch's active units of the type; otherwise of any branch.</summary>
    public Guid? BranchId { get; init; }
}
