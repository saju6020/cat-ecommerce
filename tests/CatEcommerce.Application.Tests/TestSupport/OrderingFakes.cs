using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>Hand-written in-memory doubles of the ports Ordering's handlers use (no mocking library is in scope). Unit tests only: no fake is ever
/// registered in <c>src/</c> (HO-ORD-001 §20.2).</summary>
public sealed class FakeCatalogueLookup : ICatalogueLookup
{
    public Dictionary<Guid, OrderCatalogueSku> Skus { get; } = [];
    public HashSet<Guid> ExistingProducts { get; } = [];

    public OrderCatalogueSku Add(decimal price = 100m, bool purchasable = true, bool prescription = false, string name = "Test product", int category = 5)
    {
        var sku = new OrderCatalogueSku(
            Guid.NewGuid(), Guid.NewGuid(), name, name, null, category, ProductReturnClass.Other, price, "BDT", Guid.NewGuid(), purchasable, prescription, null, false, []);
        Skus[sku.SkuId] = sku;
        ExistingProducts.Add(sku.ProductId);
        return sku;
    }

    public Task<IReadOnlyDictionary<Guid, OrderCatalogueSku>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, DateTimeOffset asOf, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, OrderCatalogueSku>>(skuIds.Where(Skus.ContainsKey).Distinct().ToDictionary(id => id, id => Skus[id]));

    public Task<OrderCatalogueSku?> GetByBarcodeAsync(string barcode, DateTimeOffset asOf, CancellationToken ct) =>
        Task.FromResult(Skus.Values.FirstOrDefault(s => s.Barcode == barcode));

    public Task<IReadOnlyList<OrderCatalogueSku>> SearchSellableAsync(string text, int take, DateTimeOffset asOf, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderCatalogueSku>>(Skus.Values.Where(s => s.IsPurchasable && s.ProductName.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(take).ToList());

    public Task<IReadOnlySet<Guid>> GetExistingProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(productIds.Where(ExistingProducts.Contains).ToHashSet());
}

public sealed class FakeSkuAvailabilityReader : ISkuAvailabilityReader
{
    public HashSet<Guid> InStock { get; } = [];

    public Task<IReadOnlyDictionary<Guid, AvailabilityStatus>> GetAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AvailabilityStatus>>(
            skuIds.Distinct().ToDictionary(id => id, id => InStock.Contains(id) ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock));
}

/// <summary>A controllable <see cref="IDietAuthorisationGate"/> for UNIT tests of the handlers (the integration tests that need the real gate wait for
/// HO-PHM-001): it authorises exactly the (cat, sku) pairs a test lists.</summary>
public sealed class FakeDietAuthorisationGate : IDietAuthorisationGate
{
    public HashSet<(Guid CatId, Guid SkuId)> Authorised { get; } = [];
    public List<(Guid CustomerId, Guid CatId, Guid SkuId)> Checks { get; } = [];

    public Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct)
    {
        Checks.Add((customerId, catId, skuId));
        return Task.FromResult(Authorised.Contains((catId, skuId))
            ? new DietGateResult(DietGateOutcome.Authorised, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "ok")
            : new DietGateResult(DietGateOutcome.NoAuthorisation, null, null, "no authorisation"));
    }

    public Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuthorisedCat>>([]);
}

public sealed class FakeCartFinder : ICartFinder
{
    public List<Guid> StaleGuestCartIds { get; } = [];
    public int LastTake { get; private set; }
    public DateTimeOffset? LastCutoff { get; private set; }

    public Task<IReadOnlyList<Guid>> FindStaleGuestCartIdsAsync(DateTimeOffset cutoff, int take, CancellationToken ct)
    {
        LastCutoff = cutoff;
        LastTake = take;
        return Task.FromResult<IReadOnlyList<Guid>>(StaleGuestCartIds.Take(take).ToList());
    }

    public Task<IReadOnlyList<Guid>> FindCartIdsByCustomerAsync(Guid customerId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
}

public static class OrderingTestSettings
{
    /// <summary>The Orders defaults of HO-ORD-001 §10.2 plus BDT.</summary>
    public static InMemoryBusinessSettings Defaults(Dictionary<string, object>? overrides = null)
    {
        var values = new Dictionary<string, object>
        {
            [SettingKeys.CommerceCurrencyCode] = "BDT",
            [SettingKeys.OrdersMaxQuantityPerLine] = 20,
            [SettingKeys.OrdersMaxCartLines] = 50,
            [SettingKeys.OrdersGuestCartRetentionDays] = 30,
            [SettingKeys.OrdersBankTransferWindowHours] = 48,
            [SettingKeys.OrdersCodEnabled] = true,
            [SettingKeys.OrdersCodMaxOrderValue] = 5000m,
            [SettingKeys.OrdersBankTransferEnabled] = true,
            [SettingKeys.OrdersPayOnCollectionEnabled] = true,
            [SettingKeys.OrdersPickupCollectionDays] = 7,
            [SettingKeys.OrdersReturnWindowDays] = 7,
        };
        if (overrides is not null) foreach (var (key, value) in overrides) values[key] = value;
        return new InMemoryBusinessSettings(values);
    }
}
