using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record StaffOriginViewModel(string CountryCode, string CountryName, string? SourcingNotes, bool IsFeatured, int SortOrder, int ProductCount);

/// <summary>HO-SHP-001 section 12.4 -- staff origins list, including origins with no products yet.</summary>
public sealed class GetStaffOriginsQuery : Query<IReadOnlyList<StaffOriginViewModel>>;
