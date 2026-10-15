using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>
/// HO-IMP-001 §12.6 / ADR-116 -- THE contract HO-SHP-001 and HO-ORD-001 consume: per SKU the quantity sellable at retail in a branch and the
/// FEFO-first expiry, read from <c>inventory.v_sellable_stock</c> (so a catalogue filter needs a join, not a ledger sum per request). A SKU with no
/// sellable stock is returned with quantity 0, so the caller never has to tell "absent" from "none".
/// </summary>
public sealed class GetSellableStockQuery : Query<IReadOnlyList<SellableStockViewModel>>
{
    public required Guid BranchId { get; init; }
    public required IReadOnlyCollection<Guid> SkuIds { get; init; }
}

/// <summary>US-IMP-07 AC1 -- what FEFO would allocate, without allocating: the same <c>IStockLedger.PlanAsync</c> a consumer calls, so the preview
/// cannot disagree with the real thing.</summary>
public sealed class GetFefoPreviewQuery : Query<FefoPreviewViewModel>
{
    public required Guid BranchId { get; init; }
    public required Guid SkuId { get; init; }
    public required decimal Quantity { get; init; }
    public required string Purpose { get; init; }
    public Guid? LocationId { get; init; }
}
