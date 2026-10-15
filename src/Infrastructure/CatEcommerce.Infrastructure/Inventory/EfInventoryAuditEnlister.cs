using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>Adds a NEW audit entry to the scoped <see cref="CatEcommerceDbContext"/> <b>without saving</b>. Never call <c>SaveChangesAsync</c> here.</summary>
public class EfInventoryAuditEnlister(CatEcommerceDbContext db) : IInventoryAuditEnlister
{
    public virtual void Enlist(AuditEntryAggregate entry) => db.AuditEntries.Add(entry);
}
