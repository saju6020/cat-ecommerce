using CatEcommerce.Domain.Aggregates.Inventory;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 section 8.13 -- enlists a NEW alert in the scoped unit of work WITHOUT saving, so the command that causes it (reporting a storage breach) commits the
/// alert with its own aggregate in one transaction. The same shape as <see cref="IBatchRegistry"/>.
/// </summary>
public interface IInventoryAlertEnlister
{
    void Enlist(InventoryAlertAggregate alert);
}
