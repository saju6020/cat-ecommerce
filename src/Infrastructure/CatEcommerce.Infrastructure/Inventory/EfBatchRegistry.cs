using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.14 / ADR-105 -- adds a NEW batch to the scoped <see cref="CatEcommerceDbContext"/> <b>without saving</b>: the goods receipt's own
/// save commits the receipt, its batches and their ledger entries together (F23). Receipts only. Never call <c>SaveChangesAsync</c> here.
/// </summary>
public sealed class EfBatchRegistry(CatEcommerceDbContext db) : IBatchRegistry
{
    public void Enlist(BatchAggregate batch) => db.Batches.Add(batch);
}
