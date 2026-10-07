using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>Column projection: bundle names joined to their component rows, never an aggregate.</summary>
public sealed class BundleMembershipReader(CatEcommerceDbContext db) : IBundleMembershipReader
{
    public async Task<IReadOnlyList<string>> FindActiveBundleNamesContainingAsync(IReadOnlyCollection<Guid> componentSkuIds, CancellationToken ct = default)
    {
        var skuIds = componentSkuIds.ToList();
        return await db.Products.AsNoTracking()
            .Where(p => p.Kind == ProductKind.Bundle && p.Status != ProductStatus.Archived
                && p.BundleComponents.Any(c => skuIds.Contains(c.ComponentSkuId)))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(ct);
    }
}
