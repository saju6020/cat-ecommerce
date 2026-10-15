namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>
/// HO-IMP-001 §8.11 / US-IMP-05 AC2 -- one row of <c>inventory.v_batch_unit_cost</c>: a batch's unit cost in base currency, <c>Landed</c> when its consignment's
/// allocation has a line for it, otherwise <c>Purchase</c>. Stock valuation, near-expiry value at risk and (HO-RPT-001) margin read it. Keyless, read-only.
/// </summary>
public sealed class BatchUnitCostEntry
{
    public Guid BatchId { get; set; }
    public decimal UnitCostBase { get; set; }
    public string CostBasis { get; set; } = default!;
}
