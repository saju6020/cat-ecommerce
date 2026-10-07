using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-I1 -- GET /api/staff/customers/duplicates.</summary>
public sealed class GetDuplicateCustomerGroupsQuery : Query<PagedResult<DuplicateCustomerGroupViewModel>>;
