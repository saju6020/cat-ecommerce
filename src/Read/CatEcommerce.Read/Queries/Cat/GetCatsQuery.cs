using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>
/// Q2 -- GET /api/cats. Paginated via the base Skip/Take (F8) -- never redeclare. Returns
/// PagedResult so the client has a total count (owner decision 2026-09-26, HO-CAT-001 §1
/// Amended -- pageSize capped at 100 in CatsController.ApplyPaging).
/// </summary>
public sealed class GetCatsQuery : Query<PagedResult<CatListItemViewModel>>
{
    public Guid? OwnerId { get; init; }
    public bool IncludeDeceased { get; init; } // default false, US-CAT-09
}
