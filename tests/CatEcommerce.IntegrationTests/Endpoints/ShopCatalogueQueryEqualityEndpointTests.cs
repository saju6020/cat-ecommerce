using System.Globalization;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001-FU4 (SHP backlog row 64) -- the catalogue's filters now run before pricing and availability (a lower(brand) expression index,
/// a per-product LATERAL price in <c>product.storefront_catalogue_entry</c>, availability as a SQL semi-join). Behaviour is unchanged: these
/// tests pin the items, order, prices, counts and paging on one seeded catalogue (own unique brand, section 16.1), and the view's pricing
/// against the previous definition's SQL.
/// </summary>
public sealed class ShopCatalogueQueryEqualityEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private sealed record Seed(string Brand, Dictionary<Guid, decimal> Price, HashSet<Guid> InStock, Dictionary<Guid, string> Category, HashSet<Guid> Bundles);

    private async Task<Seed> SeedAsync()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = $"Eq-{Guid.NewGuid():N}"[..16];
        var price = new Dictionary<Guid, decimal>();
        var inStock = new HashSet<Guid>();
        var category = new Dictionary<Guid, string>();
        var bundles = new HashSet<Guid>();

        async Task<SeededProduct> Standard(string name, string cat, decimal amount, decimal stock)
        {
            var p = await CreateProductAsync(staff, brand, name, cat, amount, stock: stock);
            price[p.ProductId] = amount; category[p.ProductId] = cat;
            if (stock > 0) inStock.Add(p.ProductId);
            return p;
        }

        var p1 = await Standard("Alpha", "HealthAndGrooming", 400m, 5m);
        var p2 = await Standard("Bravo", "ToysAndFurniture", 900m, 5m);
        var p3 = await Standard("Charlie", "HealthAndGrooming", 400m, 0m);          // ties with Alpha on price, not stocked
        var p4 = await Standard("Delta", "ToysAndFurniture", 1200m, 5m);
        var p5 = await Standard("Echo", "HealthAndGrooming", 250m, 0m);

        // Bravo: a later price supersedes the first (the latest effective one counts); a future price is ignored.
        await Task.Delay(50);
        await SchedulePriceAsync(staff, p2.SkuId, 700m);
        price[p2.ProductId] = 700m;
        await SchedulePriceAsync(staff, p2.SkuId, 100m, DateTimeOffset.UtcNow.AddDays(1));

        var bundleIn = await CreateBundleAsync(staff, brand, "Kit In", [(p1.SkuId, 1), (p4.SkuId, 1)], 2000m);
        price[bundleIn.ProductId] = 2000m; category[bundleIn.ProductId] = "HealthAndGrooming"; bundles.Add(bundleIn.ProductId); inStock.Add(bundleIn.ProductId);

        var bundleOut = await CreateBundleAsync(staff, brand, "Kit Out", [(p5.SkuId, 1), (p3.SkuId, 1)], 1500m);   // neither component has stock
        price[bundleOut.ProductId] = 1500m; category[bundleOut.ProductId] = "HealthAndGrooming"; bundles.Add(bundleOut.ProductId);

        return new Seed(brand, price, inStock, category, bundles);
    }

    private async Task<(List<JsonElement> Items, int Total)> FetchAsync(string query, int pageSize)
    {
        var items = new List<JsonElement>();
        var total = 0;
        for (var page = 1; ; page++)
        {
            var body = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?{query}&pageSize={pageSize}&pageNumber={page}"));
            total = body.GetProperty("totalCount").GetInt32();
            var batch = body.GetProperty("items").EnumerateArray().ToList();
            items.AddRange(batch);
            if (batch.Count < pageSize || items.Count >= total) break;
        }
        return (items, total);
    }

    private static List<Guid> Ids(IEnumerable<JsonElement> items) => items.Select(i => i.GetProperty("productId").GetGuid()).ToList();

    private static decimal Amount(JsonElement item) => decimal.Parse(item.GetProperty("fromPrice").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);

    [Fact]
    public async Task TheCatalogue_GivesTheSameItemsOrderPricesCountsAndPaging_ForBrandCategoryAvailabilityPriceAndSort()
    {
        var seed = await SeedAsync();
        var brand = Uri.EscapeDataString(seed.Brand);
        string[] sorts = ["newest", "price_asc", "price_desc", "popularity", "rating"];
        string[] filters =
        [
            "", "&category=ToysAndFurniture", "&category=HealthAndGrooming", "&minPrice=500&maxPrice=1300", "&availability=inStock",
            "&availability=inStock&category=HealthAndGrooming", "&availability=inStock&minPrice=500&maxPrice=1300"
        ];

        foreach (var sort in sorts)
        foreach (var filter in filters)
        {
            var query = $"brand={brand}{filter}&sort={sort}";
            var (whole, total) = await FetchAsync(query, 48);
            var (paged, pagedTotal) = await FetchAsync(query, 2);   // page boundaries: two per page, the last page short or exactly full

            Assert.Equal(Ids(whole), Ids(paged));
            Assert.Equal(total, pagedTotal);
            Assert.Equal(whole.Count, total);
            Assert.Equal(whole.Count, Ids(whole).Distinct().Count());

            var expected = seed.Price.Keys.AsEnumerable();
            if (filter.Contains("category=ToysAndFurniture")) expected = expected.Where(id => seed.Category[id] == "ToysAndFurniture");
            if (filter.Contains("category=HealthAndGrooming")) expected = expected.Where(id => seed.Category[id] == "HealthAndGrooming");
            if (filter.Contains("minPrice=500")) expected = expected.Where(id => !seed.Bundles.Contains(id) && seed.Price[id] is >= 500m and <= 1300m);
            if (filter.Contains("availability=inStock")) expected = expected.Where(seed.InStock.Contains);
            Assert.Equal(expected.OrderBy(i => i), Ids(whole).OrderBy(i => i));

            // Prices: the latest price effective now (Bravo's 700 -- never its superseded 900 or its future 100) -- and the page label agrees with the filter.
            foreach (var item in whole)
            {
                var id = item.GetProperty("productId").GetGuid();
                Assert.Equal(seed.Price[id], Amount(item));
                Assert.Equal(seed.InStock.Contains(id) ? "InStock" : "OutOfStock", item.GetProperty("availability").GetString());
            }
        }

        // Price sort: by the from-price, ties by product id (stable paging).
        var asc = (await FetchAsync($"brand={brand}&sort=price_asc", 48)).Items;
        Assert.Equal([250m, 400m, 400m, 700m, 1200m, 1500m, 2000m], asc.Select(Amount));
        var tied = Ids(asc.Skip(1).Take(2));
        Assert.Equal(tied.OrderBy(i => i), tied);
        var desc = (await FetchAsync($"brand={brand}&sort=price_desc", 48)).Items;
        Assert.Equal([2000m, 1500m, 1200m, 700m, 400m, 400m, 250m], desc.Select(Amount));

        // Bundles in the availability filter: only the bundle whose components are covered.
        var kits = (await FetchAsync($"brand={brand}&availability=inStock&q=kit", 48)).Items;
        Assert.Equal(seed.Bundles.Where(seed.InStock.Contains).ToList(), Ids(kits));
    }

    [Fact]
    public async Task TheBrandFilter_IsCaseInsensitive_AndMatchesAMultiBrandSelection()
    {
        var seed = await SeedAsync();
        var lower = (await FetchAsync($"brand={Uri.EscapeDataString(seed.Brand.ToLowerInvariant())}&sort=newest", 48)).Items;
        var upper = (await FetchAsync($"brand={Uri.EscapeDataString(seed.Brand.ToUpperInvariant())}&sort=newest", 48)).Items;
        var two = (await FetchAsync($"brand={Uri.EscapeDataString(seed.Brand)}&brand={Guid.NewGuid():N}&sort=newest", 48)).Items;

        Assert.Equal(seed.Price.Count, lower.Count);
        Assert.Equal(Ids(lower), Ids(upper));
        Assert.Equal(Ids(lower), Ids(two));
    }

    [Fact]
    public async Task TheStorefrontView_PricesEachProductExactlyAsThePreviousDefinitionDid()
    {
        var seed = await SeedAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        // The previous view's pricing subquery, verbatim, compared with the new view's columns for this test's products.
        var mismatches = await db.Database.SqlQuery<int>($"""
            WITH legacy AS (
                SELECT s.product_id, MIN(cp.amount) AS min_price, MAX(cp.amount) AS max_price, MIN(cp.currency_code) AS currency_code
                FROM product.sku s
                JOIN (
                    SELECT DISTINCT ON (sku_id) sku_id, amount, currency_code
                    FROM product.sku_price
                    WHERE effective_from <= now()
                    ORDER BY sku_id, effective_from DESC
                ) cp ON cp.sku_id = s."Id"
                WHERE s.status = 1
                GROUP BY s.product_id
            )
            SELECT COUNT(*)::int AS "Value"
            FROM product.storefront_catalogue_entry e
            LEFT JOIN legacy l ON l.product_id = e.product_id
            WHERE e.brand = {seed.Brand}
              AND (e.min_price IS DISTINCT FROM l.min_price OR e.max_price IS DISTINCT FROM l.max_price OR e.currency_code IS DISTINCT FROM l.currency_code)
            """).SingleAsync();
        Assert.Equal(0, mismatches);

        var rows = await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM product.storefront_catalogue_entry WHERE brand = {seed.Brand}").SingleAsync();
        Assert.Equal(seed.Price.Count, rows);
    }
}
