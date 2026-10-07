using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetStaffProductsQuery : Query<PagedResult<StaffProductSummaryViewModel>>
{
    public string? Q { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Brand { get; init; }
    public string? Barcode { get; init; }
    public string? Kind { get; init; }
    public bool? Listed { get; init; }
    public string? Sort { get; init; }
}
