using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Scheduling;

public sealed class WaitlistEntryFinder(CatEcommerceDbContext db) : IWaitlistEntryFinder
{
    public async Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Set<WaitlistEntryAggregate>().AsNoTracking()
            .Where(w => !w.IsMarkedToDelete && w.CustomerId == customerId)
            .Select(w => w.Id)
            .ToListAsync(ct);
}
