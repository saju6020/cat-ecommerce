using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>Rows 8, 13, 20 -- business date, interim availability guard, food recommendation cost and rate limit.</summary>
public sealed partial class ShopBacklogFixesEndpointTests
{
    // ── Row 8: the cat's age uses the business date, not the UTC date ───────────────────────────

    [Fact]
    public async Task Row8_CatAge_UsesTheMainBranchDate_NotTheUtcDate()
    {
        // 20:00 UTC is 02:00 the NEXT day in Asia/Dhaka (UTC+6): the two dates differ.
        var now = DateTimeOffset.UtcNow;
        var instant = new DateTimeOffset(now.Year, now.Month, now.Day, 20, 0, 0, TimeSpan.Zero);
        if (instant < now) instant = instant.AddDays(1);
        var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(instant);
        var clockFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fake)));

        var businessToday = BranchCalendar.LocalDateOf(instant, "Asia/Dhaka");
        var utcToday = DateOnly.FromDateTime(instant.UtcDateTime);
        Assert.NotEqual(utcToday, businessToday);

        // Exactly twelve months old on the business date (an Adult, the kitten limit being 12 months), but one day
        // short of twelve months on the UTC date (a Kitten).
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(clockFactory);
        var catId = await SeedCatAsync(customerId, "Boundary", businessToday.AddMonths(-12));

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));

        Assert.Equal("Adult", body.GetProperty("cat").GetProperty("lifeStage").GetString());
    }

    // ── Row 13: Shop:InterimAvailabilityFromPrice is explicit, and refused outside Development/Testing ─

    [Fact]
    public void Row13_TheShippedAppsettings_StatesInterimAvailabilityOff()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));

        var shop = document.RootElement.GetProperty("Shop");
        Assert.True(shop.TryGetProperty("InterimAvailabilityFromPrice", out var flag));
        Assert.Equal(JsonValueKind.False, flag.ValueKind);
    }

    [Fact]
    public void Row13_HostInProduction_WithInterimAvailabilityOn_FailsToStart()
    {
        using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // The other development-only seams pinned safe, so ONLY the Shop flag can be responsible.
                ["ConnectionStrings:CatEcommerce"] = "Host=localhost;Port=5432;Database=catecommerce_dev;Username=postgres;Password=postgres",
                ["PublicSite:WebBaseUrl"] = "https://portal.example.com",
                ["Notifications:ApiBaseUrl"] = "https://api.example.com",
                ["Captcha:TestAnswer"] = "",
                ["DevUser:Enabled"] = "false",
                ["Shop:InterimAvailabilityFromPrice"] = "true"
            }));
        });

        var exception = Record.Exception(() => production.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Shop:InterimAvailabilityFromPrice", exception.ToString());
    }

    // ── Row 20: food recommendations -- batched gate, rate limit ────────────────────────────────

    private sealed class GateCalls
    {
        public int CheckAsyncCalls;
        public int CheckManyCalls;
        public int SkusInBatches;
    }

    private sealed class CountingGate(IDietAuthorisationGate inner, GateCalls calls) : IDietAuthorisationGate
    {
        public Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct)
        {
            Interlocked.Increment(ref calls.CheckAsyncCalls);
            return inner.CheckAsync(customerId, catId, skuId, ct);
        }

        public Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct) =>
            inner.GetAuthorisedCatsAsync(customerId, skuId, ct);

        public async Task<IReadOnlyDictionary<Guid, DietGateResult>> CheckManyAsync(Guid customerId, Guid catId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
        {
            Interlocked.Increment(ref calls.CheckManyCalls);
            Interlocked.Add(ref calls.SkusInBatches, skuIds.Count);
            var results = new Dictionary<Guid, DietGateResult>();
            foreach (var skuId in skuIds) results[skuId] = await inner.CheckAsync(customerId, catId, skuId, ct);
            return results;
        }
    }

    [Fact]
    public async Task Row20_FoodRecommendations_PutAllPrescriptionSkusToTheGateInOneBatchedCall()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var calls = new GateCalls();
        var countingFactory = gateFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IDietAuthorisationGate>(sp =>
                new CountingGate(new ControllableDietAuthorisationGate(authorisations, sp.GetRequiredService<CatEcommerceDbContext>()), calls));
        }));

        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BatchRx");
        var units = NextUnitsSold();
        var products = new List<SeededProduct>();
        for (var i = 0; i < 3; i++)
        {
            var rx = await CreateProductAsync(staff, brand, $"Rx Diet {i}", "CatFood", prescription: true, lifeStage: "Adult");
            await PushUnitsSoldAsync(rx.ProductId, units);
            products.Add(rx);
        }

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(countingFactory);
        var catId = await SeedCatAsync(customerId, "Rx Cat", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        authorisations.Authorise(customerId, catId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), products[0].SkuId);

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));

        Assert.Equal(0, calls.CheckAsyncCalls);
        Assert.Equal(1, calls.CheckManyCalls);
        Assert.True(calls.SkusInBatches >= 3);
        Assert.Contains(body.GetProperty("items").EnumerateArray(), i => i.GetProperty("item").GetProperty("productId").GetGuid() == products[0].ProductId);
        Assert.DoesNotContain(body.GetProperty("items").EnumerateArray(), i => i.GetProperty("item").GetProperty("productId").GetGuid() == products[1].ProductId);
    }

    [Fact]
    public async Task Row20_FoodRecommendations_AreRateLimitedPerCustomer()
    {
        var limited = Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Shop:FoodRecommendations:PermitLimit"] = "2",
                ["Shop:FoodRecommendations:WindowMinutes"] = "10"
            })));
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(limited);
        var catId = await SeedCatAsync(customerId);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);

        // Per customer, not per address: another customer on the same address is unaffected.
        var (other, otherId) = await RegisterAndSignInCustomerAsync(limited);
        var otherCat = await SeedCatAsync(otherId);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/customers/me/cats/{otherCat}/food-recommendations")).StatusCode);
    }
}
