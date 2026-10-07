using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>Q1 -- GET /api/cats/{catId}.</summary>
public sealed class GetCatProfileQuery : Query<CatProfileViewModel?>
{
    public required Guid CatId { get; init; }
}
