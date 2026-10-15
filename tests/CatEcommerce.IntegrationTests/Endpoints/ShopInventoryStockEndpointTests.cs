using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 section 16 I-C1 ... I-C6, I-F5 (HO-SHP-001-FU2, backlog row 51). Stock is seeded exactly as Inventory holds it (section 16.1
/// rule 3): a posted goods receipt through Inventory's own endpoint, quarantine / adjustment through Inventory's own endpoints, and
/// <c>inventory.v_sellable_stock</c> derives the figure. Shop has no command, route or Testing-only endpoint that sets stock.
/// </summary>
public sealed class ShopInventoryStockEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<JsonElement> PublicDetailAsync(string slug, HttpClient? client = null) =>
        await JsonOkAsync(await (client ?? CreateClient()).GetAsync($"/api/shop/products/{slug}"));

    private static string Availability(JsonElement detail, int sku = 0) => detail.GetProperty("skus")[sku].GetProperty("availability").GetString()!;

    /// <summary>A published two-variant product (S and L, each with its own price); only the S variant is stocked, with <paramref name="stockedQuantity"/>.</summary>
    private async Task<(Guid ProductId, string Slug, Guid StockedSku, Guid UnstockedSku)> TwoVariantProductAsync(HttpClient staff, string brand, decimal stockedQuantity)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("variants");
        var create = await staff.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = "Two Sizes", brand, category = "ToysAndFurniture", taxClassId, slug }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        var skuIds = new List<Guid>();
        foreach (var (size, price) in new[] { ("S", 500m), ("L", 900m) })
        {
            var add = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
                new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("VAR"), sizeOrWeight = size }, Json);
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
            var skuId = (await add.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();
            await SchedulePriceAsync(staff, skuId, price);
            skuIds.Add(skuId);
        }

        await PublishAsync(staff, productId);
        await StockSkuAsync(skuIds[0], stockedQuantity);
        return (productId, slug, skuIds[0], skuIds[1]);
    }

    [Fact]
    public async Task I_C1_OneVariantInStock_TheOtherOutOfStock_TheProductStaysListed_AndTheStockedVariantIsPurchasable()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Variants");
        var product = await TwoVariantProductAsync(staff, brand, stockedQuantity: 5m); // variant A sellable 5, variant B never stocked
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var detail = await PublicDetailAsync(product.Slug);
        var availabilityBySku = detail.GetProperty("skus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("skuId").GetGuid(), s => s.GetProperty("availability").GetString());
        Assert.Equal("InStock", availabilityBySku[product.StockedSku]);
        Assert.Equal("OutOfStock", availabilityBySku[product.UnstockedSku]);

        // The product is still listed (one variant out of stock does not unlist it) and counts as in stock.
        var list = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}"));
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal("InStock", item.GetProperty("availability").GetString());

        var purchasability = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{product.Slug}/purchasability"));
        var purchasableBySku = purchasability.GetProperty("skus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("skuId").GetGuid(), s => s.GetProperty("purchasable").GetBoolean());
        Assert.True(purchasableBySku[product.StockedSku]);
        Assert.False(purchasableBySku[product.UnstockedSku]);
    }

    [Fact]
    public async Task I_C2_ShopHasNoStockWriteRoute_AndTheFigureIsLive_AStockChangeShowsAtOnce()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Live"), "Live Figure", stock: 0m);

        // There is nothing to push to: Shop publishes no stock route (the old push contract and its AsOf ordering are gone).
        foreach (var path in new[] { $"/api/staff/shop/skus/{product.SkuId}/stock", $"/api/staff/shop/skus/{product.SkuId}/stock-level" })
        {
            var attempt = await staff.PutAsJsonAsync(path, new { sellableQuantity = 5 }, Json);
            Assert.True(attempt.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{path} -> {(int)attempt.StatusCode}");
        }

        Assert.Equal("OutOfStock", Availability(await PublicDetailAsync(product.Slug)));

        // Inventory receives stock: the very next read shows it -- no staleness window, no copy.
        var batchId = await StockSkuAsync(product.SkuId, 5m);
        Assert.Equal("InStock", Availability(await PublicDetailAsync(product.Slug)));

        // Inventory takes it out of circulation (quarantine): the very next read shows that too.
        await SetSkuOutOfStockAsync(product.SkuId);
        Assert.Equal("OutOfStock", Availability(await PublicDetailAsync(product.Slug)));
        Assert.NotEqual(Guid.Empty, batchId);
    }

    [Fact]
    public async Task I_C3_TheExpiryNotice_ShowsInsideTheWindow_NotOutsideIt_AndFollowsTheSetting()
    {
        var staff = await SignInAsShopManagerAsync();
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var near = await CreateProductAsync(staff, NewBrand("Expiry"), "Near Expiry", stock: 0m);
        await StockSkuAsync(near.SkuId, 10m, expiresOn: today.AddDays(30), expiryTracked: true);
        var far = await CreateProductAsync(staff, NewBrand("Expiry"), "Far Expiry", stock: 0m);
        await StockSkuAsync(far.SkuId, 10m, expiresOn: today.AddDays(90), expiryTracked: true);

        async Task<JsonElement> NoticeAsync(string slug) =>
            (await PublicDetailAsync(slug)).GetProperty("skus")[0].GetProperty("expiryNotice");

        // Window 60 (the default): +30 days is inside, +90 outside.
        var nearNotice = await NoticeAsync(near.Slug);
        Assert.Equal(JsonValueKind.Object, nearNotice.ValueKind);
        Assert.Equal(today.AddDays(30).ToString("yyyy-MM-dd"), nearNotice.GetProperty("expiresOn").GetString());
        Assert.Equal(JsonValueKind.Null, (await NoticeAsync(far.Slug)).ValueKind);

        // Window 120: +90 days is inside now.
        await SetExpiryWindowAsync(adminClient, "120");
        try
        {
            var shown = await NoticeAsync(far.Slug);
            Assert.Equal(today.AddDays(90).ToString("yyyy-MM-dd"), shown.GetProperty("expiresOn").GetString());
        }
        finally
        {
            await SetExpiryWindowAsync(adminClient, "60");
        }

        // Window 0: never shown, however close.
        await SetExpiryWindowAsync(adminClient, "0");
        try
        {
            Assert.Equal(JsonValueKind.Null, (await NoticeAsync(near.Slug)).ValueKind);
        }
        finally
        {
            await SetExpiryWindowAsync(adminClient, "60");
        }
    }

    private static async Task SetExpiryWindowAsync(HttpClient admin, string days)
    {
        var response = await admin.PutAsJsonAsync("/api/admin/settings",
            new { changes = new[] { new { key = SettingKeys.ShopExpiryDisplayWindowDays, value = days } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task I_C3_TheNoticeIsTheFefoFirstBatch_NotTheLaterOne()
    {
        var staff = await SignInAsShopManagerAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var product = await CreateProductAsync(staff, NewBrand("Fefo"), "Two Batches", stock: 0m);
        await StockSkuAsync(product.SkuId, 4m, expiresOn: today.AddDays(200), expiryTracked: true);
        await StockSkuAsync(product.SkuId, 6m, expiresOn: today.AddDays(20), expiryTracked: true);

        var notice = (await PublicDetailAsync(product.Slug)).GetProperty("skus")[0].GetProperty("expiryNotice");
        Assert.Equal(today.AddDays(20).ToString("yyyy-MM-dd"), notice.GetProperty("expiresOn").GetString());
    }

    [Fact]
    public async Task I_C4_ASkuInventoryNeverReported_IsOutOfStockPublicly_AndHasNullStockForStaff_NotZero()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Never"), "Never Stocked", stock: 0m);

        Assert.Equal("OutOfStock", Availability(await PublicDetailAsync(product.Slug)));

        var staffDetail = await JsonOkAsync(await staff.GetAsync($"/api/staff/shop/products/{product.ProductId}"));
        var stock = staffDetail.GetProperty("skus")[0].GetProperty("stock");
        Assert.Equal(JsonValueKind.Null, stock.ValueKind); // "never reported", not 0
    }

    [Fact]
    public async Task I_C4_TheStaffStockField_CarriesTheSellableQuantity_TheFefoFirstExpiry_AndTheReadTime()
    {
        var staff = await SignInAsShopManagerAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var product = await CreateProductAsync(staff, NewBrand("StaffStock"), "Staff Stock", stock: 0m);
        await StockSkuAsync(product.SkuId, 7m, expiresOn: today.AddDays(45), expiryTracked: true);
        await StockSkuAsync(product.SkuId, 3m, expiresOn: today.AddDays(400), expiryTracked: true);

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var stock = (await JsonOkAsync(await staff.GetAsync($"/api/staff/shop/products/{product.ProductId}")))
            .GetProperty("skus")[0].GetProperty("stock");

        Assert.Equal(10m, stock.GetProperty("sellableQuantity").GetDecimal());
        Assert.Equal(today.AddDays(45).ToString("yyyy-MM-dd"), stock.GetProperty("nextAllocatableExpiryOn").GetString());
        Assert.True(stock.GetProperty("asOf").GetDateTimeOffset() >= before);
    }

    [Fact]
    public async Task I_C6_WhatInventoryHolds_IsExactlyWhatTheDetailShows_AcrossAZeroAndBackAgain()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Exact"), "Exact Figure", stock: 0m);
        var batchId = await StockSkuAsync(product.SkuId, 5m);

        async Task<(string Availability, decimal? Sellable)> ShownAsync()
        {
            var availability = Availability(await PublicDetailAsync(product.Slug));
            var stock = (await JsonOkAsync(await staff.GetAsync($"/api/staff/shop/products/{product.ProductId}"))).GetProperty("skus")[0].GetProperty("stock");
            return (availability, stock.ValueKind == JsonValueKind.Null ? null : stock.GetProperty("sellableQuantity").GetDecimal());
        }

        Assert.Equal(("InStock", 5m), await ShownAsync());

        await AdjustBatchAsync(product.SkuId, batchId, -5m); // sellable 0, the row stays: 5 then 0
        Assert.Equal(("OutOfStock", 0m), await ShownAsync());

        await AdjustBatchAsync(product.SkuId, batchId, 2m);
        Assert.Equal(("InStock", 2m), await ShownAsync());
    }

    [Fact]
    public async Task I_C6_AnAvailabilityFilter_AndTheListLabel_AgreeWithTheDetail()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Agree");
        var stocked = await CreateProductAsync(staff, brand, "Stocked", stock: 5m);
        var unstocked = await CreateProductAsync(staff, brand, "Unstocked", stock: 0m);
        var emptied = await CreateProductAsync(staff, brand, "Emptied", stock: 0m);
        var batchId = await StockSkuAsync(emptied.SkuId, 3m);
        await AdjustBatchAsync(emptied.SkuId, batchId, -3m);

        var all = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}"));
        var labels = all.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("productId").GetGuid(), i => i.GetProperty("availability").GetString());
        Assert.Equal("InStock", labels[stocked.ProductId]);
        Assert.Equal("OutOfStock", labels[unstocked.ProductId]);
        Assert.Equal("OutOfStock", labels[emptied.ProductId]);

        var filtered = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock"));
        var ids = filtered.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        Assert.Equal([stocked.ProductId], ids);
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task I_F5_ABundleIsInStockOnlyWhileEveryComponentCoversItsQuantity()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BundleQty");
        var a = await CreateProductAsync(staff, brand, "Component A", price: 300m, stock: 5m);
        var b = await CreateProductAsync(staff, brand, "Component B", price: 150m, stock: 1m);
        var bundle = await CreateBundleAsync(staff, brand, "Kit A2 B1", [(a.SkuId, 2), (b.SkuId, 1)]); // 2 of A, 1 of B per bundle

        async Task<(string Detail, string Catalogue, int Filtered)> ShownAsync()
        {
            var detail = Availability(await PublicDetailAsync(bundle.Slug));
            var list = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&q=kit"));
            var item = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("productId").GetGuid() == bundle.ProductId);
            var filtered = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&q=kit&availability=inStock"));
            return (detail, item.GetProperty("availability").GetString()!, filtered.GetProperty("totalCount").GetInt32());
        }

        // A 5 (covers 2), B 1 (covers 1) -> in stock.
        Assert.Equal(("InStock", "InStock", 1), await ShownAsync());

        // B 0 -> out of stock, in the detail, the label and the filter.
        await SetSkuOutOfStockAsync(b.SkuId);
        Assert.Equal(("OutOfStock", "OutOfStock", 0), await ShownAsync());

        // B back, but A drops to 1 (< the 2 a bundle needs) -> still out of stock.
        await SetSkuInStockAsync(b.SkuId);
        var aBatch = Assert.Single(StockedBatchIds(a.SkuId));
        await AdjustBatchAsync(a.SkuId, aBatch, -4m);
        Assert.Equal(("OutOfStock", "OutOfStock", 0), await ShownAsync());
    }

    [Fact]
    public async Task TheStorefrontBranchSetting_ScopesTheFigure_ToThatBranchAlone()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Branch");
        var product = await CreateProductAsync(staff, brand, "Branch Scoped", stock: 5m);
        var stockBranchId = await StockBranchIdAsync();

        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Scoped(Guid branchId) => Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Shop:StorefrontBranchId"] = branchId.ToString() })));

        // The branch that holds the stock sees it; another branch, which holds none of it, does not -- in the detail and the filter.
        var own = Scoped(stockBranchId).CreateClient();
        Assert.Equal("InStock", Availability(await PublicDetailAsync(product.Slug, own)));
        var ownList = await JsonOkAsync(await own.GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock"));
        Assert.Equal(1, ownList.GetProperty("totalCount").GetInt32());

        var other = Scoped(Guid.NewGuid()).CreateClient();
        Assert.Equal("OutOfStock", Availability(await PublicDetailAsync(product.Slug, other)));
        var otherList = await JsonOkAsync(await other.GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock"));
        Assert.Equal(0, otherList.GetProperty("totalCount").GetInt32());
    }
}
