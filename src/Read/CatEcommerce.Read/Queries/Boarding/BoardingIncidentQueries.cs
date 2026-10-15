using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

public sealed class GetIncidentsQuery : Query<IReadOnlyList<IncidentViewModel>>
{
    public required Guid BranchId { get; init; }

    /// <summary><c>Open</c> or <c>Resolved</c>; <c>null</c> lists both.</summary>
    public string? Status { get; init; }
    public Guid? StayId { get; init; }
}

/// <summary>The owner's view of one incident of their own stay; anything else is <c>null</c> (404).</summary>
public sealed class GetCustomerIncidentQuery : Query<IncidentViewModel?>
{
    public required Guid StayId { get; init; }
    public required Guid IncidentId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class GetStayDiaryQuery : Query<StayDiaryViewModel?>
{
    public required Guid StayId { get; init; }
    public required Guid CustomerId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetStayMediaQuery : Query<IReadOnlyList<DiaryMediaViewModel>?>
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetBoardingDashboardQuery : Query<BoardingDashboardViewModel>
{
    public required Guid BranchId { get; init; }
}

/// <summary>High, owner-notified, unacknowledged, unescalated incidents old enough to be worth asking the handler about.</summary>
public sealed class GetIncidentsToEscalateQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}
