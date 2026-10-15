using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

public sealed class GetCareTaskDayListQuery : Query<CareTaskDayListViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? UnitId { get; init; }

    /// <summary>Set to the caller's id for <c>assignedTo=me</c>.</summary>
    public Guid? AssignedTo { get; init; }
    public string? Status { get; init; }
}

public sealed class GetStayObservationsQuery : Query<StayObservationsViewModel?>
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Stays in house on a branch (the worker's task-generation pass takes their ids).</summary>
public sealed class GetStaysInHouseQuery : Query<IReadOnlyList<InHouseStayViewModel>>
{
    public required DateTimeOffset Now { get; init; }
}
