using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record OriginViewModel(string Code, string Name, string? SourcingNotes, int ProductCount, bool IsFeatured);

/// <summary>HO-SHP-001 section 8.6/12.4 -- US-SHP-05 AC1. Featured first, then sort order, then
/// name. Only origins with at least one Active, listed food product.</summary>
public sealed class GetOriginsQuery : Query<IReadOnlyList<OriginViewModel>>;

/// <summary>HO-SHP-001 section 12.4 -- 404 when the origin has no active food product.</summary>
public sealed class GetOriginQuery : Query<OriginViewModel?>
{
    public required string CountryCode { get; init; }
}
