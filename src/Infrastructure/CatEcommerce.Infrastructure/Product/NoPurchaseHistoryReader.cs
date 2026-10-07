using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>
/// FAKE until HO-ORD-001 lands -- replace this registration (ShopModule).
///
/// Fail-closed (ADR-086): always reports no purchases, so no customer can post a review until the
/// real purchase-history reader is registered (I-D6).
/// </summary>
public sealed class NoPurchaseHistoryReader : IPurchaseHistoryReader
{
    public Task<IReadOnlySet<Guid>> GetPurchasedSkuIdsAsync(Guid customerId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
