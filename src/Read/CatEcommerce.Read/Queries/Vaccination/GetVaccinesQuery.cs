using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Vaccination;

/// <summary>
/// Q6 -- GET /api/vaccines. Returns PagedResult so the client has a total count (owner decision
/// 2026-09-26, HO-CAT-001 §1 Amended).
/// </summary>
public sealed class GetVaccinesQuery : Query<PagedResult<VaccineListItemViewModel>>
{
    public bool ActiveOnly { get; init; }
    public bool CoreOnly { get; init; }
}
