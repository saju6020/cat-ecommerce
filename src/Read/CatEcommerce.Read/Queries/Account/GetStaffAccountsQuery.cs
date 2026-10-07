using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-F2 -- GET /api/staff-accounts.</summary>
public sealed class GetStaffAccountsQuery : Query<PagedResult<StaffAccountSummaryViewModel>>
{
    public string? Search { get; init; }
    public string? RoleId { get; init; }
}
