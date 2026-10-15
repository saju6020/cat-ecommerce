using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

/// <summary>HO-BRD-001 section 12.6. Availability is computed by the pure finder over <c>IBoardingInputs</c> -- never in SQL.</summary>
public sealed class GetBoardingAvailabilityQuery : Query<BoardingAvailabilityViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
    public required int CatCount { get; init; }

    /// <summary>Staff mode: per-unit freedom, isolation units when <see cref="RequiresIsolation"/>, no horizon.</summary>
    public bool IsStaff { get; init; }
    public bool RequiresIsolation { get; init; }
    public Guid? ExcludeStayId { get; init; }
}

/// <summary>One stay by id AND caller: a customer's own, or a staff member's active branch. Anything else is <c>null</c> (404).</summary>
public sealed class GetStayDetailQuery : Query<StayViewModel?>
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}

public sealed class GetCustomerStaysQuery : Query<PagedResult<StaySummaryViewModel>>
{
    public required Guid CustomerId { get; init; }

    /// <summary><c>upcoming</c>, <c>current</c> or <c>past</c>.</summary>
    public required string Scope { get; init; }
}

public sealed class GetStaysQuery : Query<PagedResult<StaySummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? CatId { get; init; }
}

public sealed class GetBoardingOccupancyQuery : Query<BoardingOccupancyViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}
