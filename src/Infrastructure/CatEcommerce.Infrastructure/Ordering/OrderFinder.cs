using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 §11.6 -- <see cref="IOrderFinder"/>: ids only, oldest first, no aggregate is loaded.</summary>
public sealed class OrderFinder(CatEcommerceDbContext db) : IOrderFinder
{
    public async Task<IReadOnlyList<Guid>> FindOrderIdsByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId).OrderBy(o => o.PlacedOn).Select(o => o.Id).ToListAsync(ct);

    public async Task<Guid?> FindOrderIdByTrackingNumberAsync(string trackingNumber, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(o => o.Shipment != null && o.Shipment.TrackingNumber == trackingNumber).OrderByDescending(o => o.PlacedOn).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
}
