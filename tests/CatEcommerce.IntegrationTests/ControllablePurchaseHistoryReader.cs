using System.Collections.Concurrent;
using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-SHP-001 §16.1 rule 4 -- a test-only double a test fills per customer id, answering
/// <see cref="IPurchaseHistoryReader"/> without a real HO-ORD-001. The shipped
/// <c>NoPurchaseHistoryReader</c> default is exercised separately (I-D6) by NOT installing this
/// replacement.</summary>
public sealed class ControllablePurchaseHistoryReader : IPurchaseHistoryReader
{
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _purchasedSkusByCustomer = new();

    public void SetPurchased(Guid customerId, params Guid[] skuIds) =>
        _purchasedSkusByCustomer[customerId] = [.. skuIds];

    public Task<IReadOnlySet<Guid>> GetPurchasedSkuIdsAsync(Guid customerId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        if (!_purchasedSkusByCustomer.TryGetValue(customerId, out var purchased))
            return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        var result = skuIds.Where(purchased.Contains).ToHashSet();
        return Task.FromResult<IReadOnlySet<Guid>>(result);
    }
}
