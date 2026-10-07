using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-F3 -- GET /api/staff-accounts/{id}.</summary>
public sealed class GetStaffAccountQuery : Query<StaffAccountViewModel?>
{
    public required Guid StaffAccountId { get; init; }
}
