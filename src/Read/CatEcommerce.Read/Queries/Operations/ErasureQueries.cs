using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Operations;

/// <summary>HO-XCT-001 section 12.6 Q6 -- paged ledger list, newest first. <c>Status</c> is an <c>ErasureRequestStatus</c> display name.</summary>
public sealed class GetErasureRequestsQuery : Query<PagedResult<ErasureRequestViewModel>>
{
    public string? Status { get; init; }
}

public sealed class GetErasureRequestQuery : Query<ErasureRequestViewModel?>
{
    public required Guid CustomerAccountId { get; init; }
}
