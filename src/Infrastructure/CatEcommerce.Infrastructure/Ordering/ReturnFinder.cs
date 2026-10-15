using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 §8.10 -- <see cref="IReturnFinder"/>: projections over <c>ordering.return_request</c> and <c>return_line</c>.</summary>
public sealed class ReturnFinder(CatEcommerceDbContext db) : IReturnFinder
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetOpenQuantitiesAsync(Guid orderId, CancellationToken ct)
    {
        var rows = await db.ReturnRequests.AsNoTracking()
            .Where(r => r.OrderId == orderId && (r.Status == ReturnStatus.Requested || r.Status == ReturnStatus.Approved))
            .SelectMany(r => r.Lines.Select(l => new { l.OrderLineId, l.Quantity }))
            .ToListAsync(ct);

        return rows.GroupBy(r => r.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
    }

    public async Task<IReadOnlyList<Guid>> FindReturnIdsByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.ReturnRequests.AsNoTracking().Where(r => r.CustomerId == customerId).OrderBy(r => r.RequestedOn).Select(r => r.Id).ToListAsync(ct);
}
