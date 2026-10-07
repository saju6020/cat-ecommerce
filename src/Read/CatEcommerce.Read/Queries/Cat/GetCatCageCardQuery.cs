using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>Q7 -- feeds the QR endpoint (US-CAT-11).</summary>
public sealed class GetCatCageCardQuery : Query<CatCageCardViewModel?>
{
    public required Guid CatId { get; init; }
}
