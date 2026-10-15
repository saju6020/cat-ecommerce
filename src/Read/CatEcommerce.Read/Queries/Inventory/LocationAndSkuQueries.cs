using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>HO-IMP-001 §12.4 -- the active branch's locations (<c>Stock.View</c>).</summary>
public sealed class GetStockLocationsQuery : Query<IReadOnlyList<StockLocationViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>SKU lookup for receiving: by barcode (exact) or by text (at most 20 rows), each with its <c>storage</c> object.</summary>
public sealed class GetSkuLookupQuery : Query<IReadOnlyList<SkuLookupViewModel>>
{
    public string? Search { get; init; }
    public string? Barcode { get; init; }
}

public sealed class GetStorageProfileQuery : Query<StorageProfileViewModel?>
{
    public required Guid ProductId { get; init; }
}
