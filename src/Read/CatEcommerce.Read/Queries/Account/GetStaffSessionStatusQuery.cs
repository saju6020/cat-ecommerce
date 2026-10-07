using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-G1 -- GET /api/staff/session, and the session middleware. Never extends anything.</summary>
public sealed class GetStaffSessionStatusQuery : Query<StaffSessionStatusViewModel?>
{
    public required string TokenHash { get; init; }
    public required DateTimeOffset AsOf { get; init; }
    public required int IdleTimeoutMinutes { get; init; }
}
