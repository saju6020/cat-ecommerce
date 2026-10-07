using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>
/// Q3 -- GET /api/cats/{catId}/weights. Returns PagedResult so the client has a total count
/// (owner decision 2026-09-26, HO-CAT-001 §1 Amended).
/// </summary>
public sealed class GetCatWeightHistoryQuery : Query<PagedResult<CatWeightPointViewModel>>
{
    public required Guid CatId { get; init; }
}
