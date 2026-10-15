using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>Adds a NEW alert to the scoped <see cref="CatEcommerceDbContext"/> <b>without saving</b> (F23). Never call <c>SaveChangesAsync</c> here.</summary>
public sealed class EfInventoryAlertEnlister(CatEcommerceDbContext db) : IInventoryAlertEnlister
{
    public void Enlist(InventoryAlertAggregate alert) => db.InventoryAlerts.Add(alert);
}
