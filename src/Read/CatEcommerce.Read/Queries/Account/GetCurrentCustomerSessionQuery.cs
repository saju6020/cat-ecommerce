using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-C1 -- GET /api/auth/session, and the session middleware.</summary>
public sealed class GetCurrentCustomerSessionQuery : Query<CustomerSessionViewModel?>
{
    public required string TokenHash { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}
