namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>
/// HO-IMP-001 §12.1 -- one row of <c>inventory.stock_position</c>, the projection of the ledger. Mapped KEYLESS and
/// <c>ExcludeFromMigrations</c>: EF can query it and can never write it (the table is created by raw SQL and written only by the ledger
/// triggers, ADR-099). A class with settable members so EF can compose <c>Where</c>/<c>Join</c> over it.
/// </summary>
public sealed class StockPositionEntry
{
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public Guid SkuId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public DateTimeOffset LastMovementOn { get; set; }
}
