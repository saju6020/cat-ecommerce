using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 section 12.6 (Phase F). A query never writes.

/// <summary>
/// The HO-BIL-001 contract (ADR-127): the billable, non-reversed treatment lines of a <b>finalised</b> visit, each with the price and tax class
/// snapshotted when it was recorded. Not branch-scoped -- Billing is an in-process consumer, not a staff caller (it has no active branch of its
/// own); the caller is a handler that already holds the visit id from <c>VisitFinalisedEvent</c>. Null when the visit does not exist or is not
/// finalised yet: a line can still be reversed until then, so nothing is billable.
/// </summary>
public sealed class GetVisitChargeLinesQuery : Query<VisitChargeLinesViewModel?>
{
    public required Guid VisitId { get; init; }
}

/// <summary>Section 12.4 <c>GET /api/staff/dispensary/items?search</c> -- the SKUs the active branch's clinic dispensary holds (no search text),
/// or the active SKUs matching the text (so an item that is out of stock can still be found and recorded as a shortfall, AC2).</summary>
public sealed class GetDispensaryItemsQuery : Query<PagedResult<DispensaryItemViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Search { get; init; }
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
}

/// <summary>Section 12.4 <c>GET /api/staff/dispensary/items/{skuId}/batches</c> -- the batches of one SKU the dispensary holds that Inventory
/// would let a vet use, FEFO order, the default marked. An expired, recalled or quarantined batch is not offered.</summary>
public sealed class GetIssuableBatchesQuery : Query<IReadOnlyList<IssuableBatchViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid SkuId { get; init; }
}
