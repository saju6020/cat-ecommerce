using System.Diagnostics;
using System.Net;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 §16 I-E11 -- NFR-PRF-2 / US-SHP-04 AC3: <b>p95 &lt;= 500 ms</b> at 20,000 SKUs, measured
/// per request inside the test process, over a mix of search, one to four filters and every sort,
/// eight concurrent clients. Opt-in (<c>[Trait("Category","Performance")]</c>): the §17 commands are
/// <c>--filter "Category!=Performance"</c> for the normal run and <c>--filter "Category=Performance"</c>
/// for this one.
///
/// <b>The fixture</b> (§16.1 rule 6): brand "zz Perf Fixture", 5,000 Active listed products x 4 SKUs =
/// 20,000 SKUs, each priced now, across all six categories, 12 origins and every life stage and
/// protein, created through the aggregates and their repositories (no raw SQL), seeded once and
/// idempotently -- it resumes from however many already exist and is never deleted (OQ-15). "Stocked"
/// is Inventory's: every fixture SKU without sellable stock in <c>inventory.v_sku_sellable</c> at the storefront (main) branch is received through
/// Inventory's goods-receipt endpoint (200 lines per receipt), also idempotently (HO-SHP-001-FU2).
///
/// §20.4: if this fails the query, the view or the indexes change -- never this test, the fixture
/// size or a cache in front of the list.
/// </summary>
[Trait("Category", "Performance")]
public sealed class ShopCataloguePerformanceTests(ShopApiFactory factory, ITestOutputHelper output) : ShopIntegrationTestBase(factory)
{
    private const string FixtureBrand = "zz Perf Fixture";
    private const int FixtureProducts = 5_000;
    private const int SkusPerProduct = 4;
    private const int Requests = 300;
    private const int Concurrency = 8;
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);

    private static readonly string[] Origins = ["DE", "FR", "IT", "ES", "GB", "NL", "BE", "AT", "SE", "DK", "PL", "TH"];
    private static readonly ProductCategory[] Categories = [.. Enumeration.GetAll<ProductCategory>()];
    private static readonly LifeStage[] LifeStages = [.. Enumeration.GetAll<LifeStage>()];
    private static readonly ProteinSource[] Proteins = [.. Enumeration.GetAll<ProteinSource>()];
    private static readonly string[] Flavours = ["Salmon", "Chicken", "Tuna", "Duck", "Beef", "Turkey"];

    [Fact]
    public async Task I_E11_CatalogueP95_AtTwentyThousandSkus_IsWithinBudget()
    {
        await EnsureFixtureAsync();

        // The shared rate limit (120/min/IP, I-E10) would throttle a 300-request run from one TestServer
        // address; the limit itself is not what is measured here.
        var perfFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Shop:Catalogue:PermitLimit"] = "1000000" })));

        await using (var scope = perfFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var skuCount = await db.Products.AsNoTracking().Where(p => p.Brand == FixtureBrand).SelectMany(p => p.Skus).CountAsync();
            Assert.True(skuCount >= FixtureProducts * SkusPerProduct, $"The fixture holds only {skuCount} SKUs.");
        }

        var urls = BuildRequestMix();
        var client = perfFactory.CreateClient();

        // One warm-up of each distinct shape (plan caching, JIT) -- not counted, not hidden: the timed
        // run below is the full 300.
        foreach (var url in urls.Take(12)) (await client.GetAsync(url)).EnsureSuccessStatusCode();

        var timings = new List<(string Url, double Ms)>();
        var gate = new SemaphoreSlim(Concurrency);
        var tasks = urls.Select(async url =>
        {
            await gate.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await client.GetAsync(url);
                await response.Content.ReadAsStringAsync();
                stopwatch.Stop();

                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} for {url}");
                lock (timings) timings.Add((url, stopwatch.Elapsed.TotalMilliseconds));
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        var sorted = timings.OrderBy(t => t.Ms).ToList();
        var p50 = sorted[(int)(sorted.Count * 0.50)].Ms;
        var p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1].Ms;
        var max = sorted[^1];

        var summary = $"I-E11 p95 = {p95:F1} ms (p50 = {p50:F1} ms, max = {max.Ms:F1} ms on {max.Url}) over {sorted.Count} requests, {Concurrency} concurrent, budget {Budget.TotalMilliseconds} ms";
        output.WriteLine(summary);
        Console.WriteLine(summary);

        // Where the time goes: the availability filter joins Inventory's view, everything else does not.
        foreach (var (label, subset) in new[] { ("with availability=inStock", sorted.Where(t => t.Url.Contains("availability=inStock")).ToList()), ("without", sorted.Where(t => !t.Url.Contains("availability=inStock")).ToList()) })
        {
            if (subset.Count == 0) continue;
            var split = $"I-E11 {label}: n = {subset.Count}, p50 = {subset[subset.Count / 2].Ms:F1} ms, p95 = {subset[(int)Math.Ceiling(subset.Count * 0.95) - 1].Ms:F1} ms";
            output.WriteLine(split);
            Console.WriteLine(split);
        }

        Assert.Equal(Requests, sorted.Count);
        Assert.True(p95 <= Budget.TotalMilliseconds, summary);
    }

    private static List<string> BuildRequestMix()
    {
        var sorts = new[] { "relevance", "newest", "price_asc", "price_desc", "popularity", "rating" };
        var terms = new[] { "perf", "fixture", "perf fixtur", "salmon", "chicken", "tuna", "zz perf", "duck dinner" };
        var random = new Random(20_000);
        var urls = new List<string>();

        for (var i = 0; i < Requests; i++)
        {
            var parts = new List<string> { $"brand={Uri.EscapeDataString(FixtureBrand)}" };
            var filterCount = random.Next(0, 5); // 0..4 filters besides the brand

            if (random.Next(3) > 0) parts.Add($"q={Uri.EscapeDataString(terms[random.Next(terms.Length)])}");
            for (var f = 0; f < filterCount; f++)
            {
                switch (random.Next(7))
                {
                    case 0: parts.Add($"category={Categories[random.Next(Categories.Length)].DisplayName}"); break;
                    case 1: parts.Add($"origin={Origins[random.Next(Origins.Length)]}"); break;
                    case 2: parts.Add($"lifeStage={LifeStages[random.Next(LifeStages.Length)].DisplayName}"); break;
                    case 3: parts.Add($"protein={Proteins[random.Next(Proteins.Length)].DisplayName}"); break;
                    case 4: parts.Add($"minPrice={random.Next(100, 900)}&maxPrice={random.Next(1000, 6000)}"); break;
                    case 5: parts.Add("availability=inStock"); break;
                    default: parts.Add($"minRating={random.Next(1, 4)}"); break;
                }
            }

            parts.Add($"sort={sorts[i % sorts.Length]}");
            parts.Add($"pageNumber={random.Next(1, 4)}");
            urls.Add("/api/shop/products?" + string.Join("&", parts.Distinct()));
        }

        return urls;
    }

    private async Task EnsureFixtureAsync()
    {
        int existing;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            existing = await db.Products.AsNoTracking().CountAsync(p => p.Brand == FixtureBrand);
        }

        if (existing >= FixtureProducts)
        {
            output.WriteLine($"I-E11 fixture already holds {existing} products; not reseeding.");
            await EnsureFixtureStockAsync();
            return;
        }

        output.WriteLine($"I-E11 fixture: seeding {FixtureProducts - existing} products ({existing} exist).");
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("PERF"));
        var userContext = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        const int batchSize = 100;
        for (var start = existing; start < FixtureProducts; start += batchSize)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var products = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<ProductAggregate>>();
            var prices = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<SkuPriceAggregate>>();

            for (var index = start; index < Math.Min(start + batchSize, FixtureProducts); index++)
            {
                var now = DateTimeOffset.UtcNow;
                var category = Categories[index % Categories.Length];
                var product = new ProductAggregate(Guid.NewGuid());
                product.Create(new ProductCreateDto(
                    ProductKind.Standard, $"Perf Fixture {Flavours[index % Flavours.Length]} {index:00000}", FixtureBrand, category, null,
                    taxClassId, $"zz-perf-fixture-{index:00000}", null, null, userContext, Guid.NewGuid()));

                if (category == ProductCategory.CatFood)
                {
                    product.UpdateDetails(new ProductDetailsUpdateDto(
                        product.Name, FixtureBrand, "", category, null, [], taxClassId, Origins[index % Origins.Length], true, true, [], userContext, Guid.NewGuid()));
                    product.SetFoodDetails(new FoodDetails(
                        "Perf Foods", LifeStages[index % LifeStages.Length], Proteins[index % Proteins.Length],
                        $"{Flavours[index % Flavours.Length].ToLowerInvariant()}, rice", null, null, null), userContext, Guid.NewGuid());
                }
                else if (index % 3 == 0)
                {
                    product.UpdateDetails(new ProductDetailsUpdateDto(
                        product.Name, FixtureBrand, "", category, null, [], taxClassId, Origins[index % Origins.Length], true, false, [], userContext, Guid.NewGuid()));
                }

                var skuIds = new List<Guid>();
                for (var s = 0; s < SkusPerProduct; s++)
                {
                    var skuId = Guid.NewGuid();
                    skuIds.Add(skuId);
                    product.AddSku(new SkuAddDto(skuId, $"PF{index:00000}S{s}", null, $"{(s + 1) * 250} g", null, null, null), userContext, Guid.NewGuid());
                }

                product.Publish(new PublicationFacts(skuIds, true, []), now, userContext, Guid.NewGuid());
                await products.SaveAsync(product, EventPublishOption.DoNotPublish);

                for (var s = 0; s < skuIds.Count; s++)
                {
                    var price = new SkuPriceAggregate(Guid.NewGuid());
                    price.Create(new SkuPriceCreateDto(
                        skuIds[s], new Money(300m + (index % 50) * 40m + s * 120m, "BDT"), now, userContext.UserId, now, userContext, Guid.NewGuid()), "BDT");
                    await prices.SaveAsync(price, EventPublishOption.DoNotPublish);
                }
            }
        }

        await EnsureFixtureStockAsync();
    }

    /// <summary>Stocks every fixture SKU Inventory has no sellable row for, 200 per receipt (<c>InventoryRules.MaxReceiptLines</c>).</summary>
    private async Task EnsureFixtureStockAsync()
    {
        List<(Guid SkuId, bool ExpiryTracked)> unstocked;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var storefrontBranchId = CatEcommerce.Shared.Common.Constants.AdministrationSeed.MainBranchId;
            var rows = await db.Products.AsNoTracking().Where(p => p.Brand == FixtureBrand)
                .SelectMany(p => p.Skus, (p, s) => new { SkuId = s.Id, p.Category })
                .Where(r => !db.SkuSellable.Any(v => v.SkuId == r.SkuId && v.BranchId == storefrontBranchId && v.SellableQuantity > 0))
                .ToListAsync();
            unstocked = rows.Select(r => (r.SkuId, r.Category == ProductCategory.CatFood)).ToList();
        }

        output.WriteLine($"I-E11 fixture: stocking {unstocked.Count} SKUs through goods receipts.");
        foreach (var chunk in unstocked.Chunk(InventoryRules.MaxReceiptLines))
            await StockSkusAsync(chunk);
    }
}
