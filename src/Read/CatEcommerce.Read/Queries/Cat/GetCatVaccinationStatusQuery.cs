using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>Q4 -- GET /api/cats/{catId}/vaccination-status. US-CAT-07.</summary>
public sealed class GetCatVaccinationStatusQuery : Query<CatVaccinationStatusViewModel?>
{
    public required Guid CatId { get; init; }
}
