using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record SkuByBarcodeViewModel(Guid SkuId, string Code, string Label, Guid ProductId, string ProductName, string Status);

/// <summary>HO-SHP-001 section 8.12/12.4 -- contract query (HW-2), consumed by HO-IMP-001 and
/// HO-ORD-001 in their own command handlers, and exposed here to staff.</summary>
public sealed class FindSkuByBarcodeQuery : Query<SkuByBarcodeViewModel?>
{
    public required string Barcode { get; init; }
}
