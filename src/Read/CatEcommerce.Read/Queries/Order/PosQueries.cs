using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>HO-ORD-001 §8.11 step 1 (US-ORD-16 AC2) -- a barcode scanned at the till: the SKU, its price, what the branch can sell and the FEFO batch the next unit would
/// come from. Null (-> <c>404</c>) when no purchasable SKU carries the barcode.</summary>
public sealed class GetPosSkuQuery : Query<PosSkuViewModel?>
{
    public required Guid BranchId { get; init; }
    public required string Barcode { get; init; }
}

/// <summary>The manual fallback when a barcode will not scan: purchasable SKUs whose code, name or barcode contains the text.</summary>
public sealed class SearchPosSkusQuery : Query<IReadOnlyList<PosSkuViewModel>>
{
    public required Guid BranchId { get; init; }
    public required string Text { get; init; }
}

public sealed record CounterQuoteLine(Guid SkuId, int Quantity, Guid? CatId);

/// <summary>The counter quote -- the sale priced exactly as completing it would price it, without any state change.</summary>
public sealed class GetCounterQuoteQuery : Query<CounterQuoteViewModel>
{
    public required Guid BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public required IReadOnlyList<CounterQuoteLine> Lines { get; init; }
    public string? DiscountCode { get; init; }
}

/// <summary>The receipt of a finished counter sale (structured, plus the 48-column text). Null (-> <c>404</c>) when it is not a counter sale of this branch.</summary>
public sealed class GetReceiptQuery : Query<ReceiptViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid OrderId { get; init; }
}
