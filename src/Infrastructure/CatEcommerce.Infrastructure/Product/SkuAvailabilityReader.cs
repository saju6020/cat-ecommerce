using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>HO-SHP-001 section 8.7 -- <see cref="ISkuAvailabilityReader"/> over <see cref="SkuSellability"/>,
/// the one availability seam shared with every read-side handler. Fail-closed unless
/// <c>Shop:InterimAvailabilityFromPrice</c> is on (<see cref="ShopOptions.InterimAvailabilityFromPrice"/>).</summary>
public sealed class SkuAvailabilityReader(CatEcommerceDbContext db, TimeProvider timeProvider, IOptions<ShopOptions> options) : ISkuAvailabilityReader
{
    public async Task<IReadOnlyDictionary<Guid, AvailabilityStatus>> GetAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) =>
        await SkuSellability.AvailabilityAsync(db, skuIds, timeProvider.GetUtcNow(), options.Value.InterimAvailabilityFromPrice, ct);
}
