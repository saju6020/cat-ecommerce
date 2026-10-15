using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>HO-BIL-001 section 8.11 -- creates a customer loyalty account on first use without ever colliding with a concurrent creator: the row
/// (balance zero, so nothing is lost if another request wins) is inserted with <c>ON CONFLICT DO NOTHING</c> and then read back tracked.</summary>
public sealed class EfLoyaltyAccountStore(CatEcommerceDbContext db, IUserContextProvider userContextProvider) : ILoyaltyAccountStore
{
    public void Stage(LoyaltyEntryAggregate entry) => db.Set<LoyaltyEntryAggregate>().Add(entry);

    public async Task<LoyaltyAccountAggregate> EnsureAsync(Guid customerId, CancellationToken ct = default)
    {
        var existing = await db.Set<LoyaltyAccountAggregate>().FirstOrDefaultAsync(a => a.Id == customerId, ct);
        if (existing is not null) return existing;

        var created = new LoyaltyAccountAggregate(customerId);
        created.Open(userContextProvider.GetUserContext());

        await RawInsert.InsertIgnoringConflictAsync(db, created, ["Id"], ct);

        return await db.Set<LoyaltyAccountAggregate>().FirstOrDefaultAsync(a => a.Id == customerId, ct)
            ?? throw new InvalidOperationException("The loyalty account could not be created.");
    }
}
