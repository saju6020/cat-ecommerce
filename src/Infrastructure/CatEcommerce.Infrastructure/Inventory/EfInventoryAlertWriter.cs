using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 section 11.4 C15 -- saves a new alert and answers <c>false</c> when the unique index on its dedup key or on the live (kind, SKU, location) pair
/// refuses it: two raises raced and exactly one row is wanted. Only those two constraints are treated as "already raised"; any other failure propagates. The same shape
/// as <c>EfNotificationRequestStore</c>.
/// </summary>
public sealed class EfInventoryAlertWriter(CatEcommerceDbContext db) : IInventoryAlertWriter
{
    private static readonly HashSet<string> DedupIndexes = ["ix_stock_alert_dedup_key", "ix_stock_alert_one_live_per_pair"];

    public async Task<bool> TryRaiseAsync(InventoryAlertAggregate alert, CancellationToken ct)
    {
        db.InventoryAlerts.Add(alert);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } name } && DedupIndexes.Contains(name))
        {
            db.Entry(alert).State = EntityState.Detached;
            return false;
        }
    }
}
