using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Administration;

public sealed class GetBranchesQuery : Query<IReadOnlyList<BranchSummaryViewModel>>;

public sealed class GetAdminBranchesQuery : Query<IReadOnlyList<AdminBranchViewModel>>;

public sealed class GetAdminBranchQuery : Query<AdminBranchViewModel?>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetBranchClosuresQuery : Query<IReadOnlyList<BranchClosureViewModel>>
{
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetRoomsQuery : Query<IReadOnlyList<RoomViewModel>>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetBranchCalendarQuery : Query<IReadOnlyList<TimeIntervalViewModel>>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}

public sealed class GetStaffBranchAssignmentQuery : Query<StaffBranchAssignmentViewModel?>
{
    public required Guid StaffAccountId { get; init; }
}

public sealed class GetMyBranchesQuery : Query<StaffBranchAssignmentViewModel?>
{
    public required Guid StaffAccountId { get; init; }
}
