using CatEcommerce.Domain.Aggregates.Audit;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 section 8.12 -- enlists a NEW audit entry in the scoped unit of work WITHOUT saving, so a command's own save commits the entry together with its aggregate
/// change and its outbox rows: the audit row can never be lost to a second commit that fails after the first succeeded (SRS C-9). The same shape as
/// <see cref="IInventoryAlertEnlister"/>.
/// </summary>
public interface IInventoryAuditEnlister
{
    void Enlist(AuditEntryAggregate entry);
}

/// <summary>
/// Raises one inventory alert and reports whether it was NEW: <c>false</c> when the database's unique index on the alert's dedup key (or on the live low-stock / ledger
/// mismatch pair) says an identical alert already exists -- a race the dedup pre-check could not see. Implemented in Infrastructure, which alone knows the constraint names.
/// </summary>
public interface IInventoryAlertWriter
{
    Task<bool> TryRaiseAsync(CatEcommerce.Domain.Aggregates.Inventory.InventoryAlertAggregate alert, CancellationToken ct);
}
