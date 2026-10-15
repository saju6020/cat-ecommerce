using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>HO-SHP-001 section 8.7 -- <see cref="ISkuAvailabilityReader"/> over <see cref="SkuSellability"/>,
/// the one availability seam shared with every read-side handler: <c>inventory.v_sku_sellable</c> (ADR-116)
/// for the configured storefront branch (<see cref="ShopOptions.StorefrontBranchId"/>).</summary>
public sealed class SkuAvailabilityReader(CatEcommerceDbContext db, TimeProvider timeProvider, IOptions<ShopOptions> options) : ISkuAvailabilityReader
{
    public async Task<IReadOnlyDictionary<Guid, AvailabilityStatus>> GetAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) =>
        await SkuSellability.AvailabilityAsync(db, skuIds, timeProvider.GetUtcNow(), options.Value.EffectiveStorefrontBranchId, ct);
}
