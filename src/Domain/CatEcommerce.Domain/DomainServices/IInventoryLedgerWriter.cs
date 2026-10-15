using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.8 -- used ONLY by Inventory's own handlers (Receipt, Transfer*, Adjustment*, Count*, Wastage). Validates each
/// draft (SKU/batch/location, purpose permitted at the location's kind) and ENLISTS ledger entries in the scoped unit of work
/// without saving; the caller's own repository save commits them with its aggregate (F23). Never throws for a business rule.
/// </summary>
public interface IInventoryLedgerWriter
{
    Task<StockResult<Unit>> AppendAsync(IReadOnlyList<StockMovementDraft> drafts, CancellationToken ct);
}
