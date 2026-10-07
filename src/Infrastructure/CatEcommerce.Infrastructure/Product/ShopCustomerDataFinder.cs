using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>HO-SHP-001 section 11.6 -- the "find every matching row" gap for the merge,
/// anonymisation and discontinuation handlers. Ids only, <c>AsNoTracking</c>.</summary>
public sealed class ShopCustomerDataFinder(CatEcommerceDbContext db) : IShopCustomerDataFinder
{
    public async Task<IReadOnlyList<Guid>> FindStockAlertIdsAsync(Guid customerId, CancellationToken ct = default) =>
        await db.StockAlerts.AsNoTracking().Where(a => a.CustomerId == customerId).Select(a => a.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindActiveReviewIdsAsync(Guid customerId, CancellationToken ct = default) =>
        await db.ProductReviews.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status != ProductReviewStatus.Withdrawn)
            .Select(r => r.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindUnpublishedReviewIdsAsync(Guid customerId, CancellationToken ct = default) =>
        await db.ProductReviews.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status != ProductReviewStatus.Published)
            .Select(r => r.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindPendingStockAlertIdsBySkuAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) =>
        await db.StockAlerts.AsNoTracking()
            .Where(a => a.Status == StockAlertStatus.Pending && skuIds.Contains(a.SkuId))
            .Select(a => a.Id).ToListAsync(ct);
}
