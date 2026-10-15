using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 §11.4 -- <see cref="ICartFinder"/>: id lookups over <c>ordering.cart</c>, always projected.</summary>
public sealed class CartFinder(CatEcommerceDbContext db) : ICartFinder
{
    public async Task<IReadOnlyList<Guid>> FindStaleGuestCartIdsAsync(DateTimeOffset cutoff, int take, CancellationToken ct) =>
        await db.Carts.AsNoTracking()
            .Where(c => c.OwnerKind == CartOwnerKind.Guest && c.Status == CartStatus.Active && c.LastActivityOn < cutoff)
            .OrderBy(c => c.LastActivityOn)
            .Take(take)
            .Select(c => c.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindCartIdsByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Carts.AsNoTracking().Where(c => c.CustomerId == customerId).Select(c => c.Id).ToListAsync(ct);
}
