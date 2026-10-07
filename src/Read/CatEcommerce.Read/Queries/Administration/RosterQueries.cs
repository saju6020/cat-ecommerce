using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Administration;

public sealed class GetBranchRostersQuery : Query<IReadOnlyList<StaffRosterViewModel>>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetRosterExceptionsQuery : Query<IReadOnlyList<RosterExceptionViewModel>>
{
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetStaffAvailabilityQuery : Query<IReadOnlyList<StaffAvailableIntervalViewModel>>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public Guid? StaffAccountId { get; init; }
    public bool RestrictToOpeningHours { get; init; }
}

public sealed class GetMyLeaveRequestsQuery : Query<IReadOnlyList<LeaveRequestViewModel>>
{
    public required Guid StaffAccountId { get; init; }
}

public sealed class GetLeaveRequestsQuery : Query<PagedResult<LeaveRequestViewModel>>
{
    public string? Status { get; init; }
    public Guid? StaffAccountId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
