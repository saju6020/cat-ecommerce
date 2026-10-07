using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-H1 -- GET /api/staff/customers/{id}.</summary>
public sealed class GetStaffCustomerQuery : Query<StaffCustomerViewModel?>
{
    public required Guid CustomerId { get; init; }
}
