using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-H2 -- GET /api/staff/customers?mobile=&amp;email=.</summary>
public sealed class FindCustomersQuery : Query<IReadOnlyList<StaffCustomerSummaryViewModel>>
{
    public string? Mobile { get; init; }
    public string? Email { get; init; }
}
