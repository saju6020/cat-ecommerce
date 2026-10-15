using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

public sealed class GetClinicalHistorySummaryQuery : Query<ClinicalHistorySummaryViewModel?>
{
    public required Guid CatId { get; init; }
}

/// <summary>A section by key. An unknown key is <c>null</c>; a known key with no provider is <c>available: false</c>.</summary>
public sealed class GetClinicalHistorySectionQuery : Query<ClinicalHistorySectionViewModel?>
{
    public required Guid CatId { get; init; }
    public required string Section { get; init; }
}
