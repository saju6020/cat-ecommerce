using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase B -- delivery zones, couriers, the bank-transfer account and discount codes through real HTTP: I-B1 (zone, service levels,
/// bands, resolve), I-B2 (discount code CRUD as shop manager; refused to an inventory officer), plus the uniqueness mappings of §12.5 and the
/// role table of §12.3 on these endpoints.
/// </summary>
public sealed class OrderingConfigurationEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private static object ZoneBody(
        Guid branchId, string? name = null, string district = "Dhaka", string[]? postalCodes = null, int priority = 1, bool cod = true, decimal? codMax = null) => new
    {
        branchId, name = name ?? "Zone " + Guid.NewGuid().ToString("N")[..10], priority, isActive = true, codEnabled = cod, codMaxOrderValue = codMax,
        coverage = new[] { new { district, postalCodes = postalCodes ?? [] } },
        services = new object[]
        {
            new
            {
                serviceLevel = "Standard", isActive = true, baseCharge = 60m, freeOverOrderValue = 2000m, leadTimeDays = 1,
                weightBands = new object[] { new { upToGrams = 2000, surcharge = 0m }, new { upToGrams = 5000, surcharge = 40m }, new { upToGrams = (int?)null, surcharge = 100m } },
                slotWindows = new object[] { new { dayOfWeek = "Monday", start = "10:00", end = "13:00" }, new { dayOfWeek = "Tuesday", start = "14:00", end = "17:00" } },
            },
            new { serviceLevel = "Express", isActive = true, baseCharge = 150m, freeOverOrderValue = (decimal?)null, leadTimeDays = 0, weightBands = Array.Empty<object>(), slotWindows = Array.Empty<object>() },
        },
    };

    private async Task<Guid> CreateZoneAsync(OrderingContext context, object body)
    {
        var response = await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", body, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("zoneId").GetGuid();
    }

    // ── I-B1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_B1_ACreatedZone_ReturnsItsServiceLevelsBandsAndWindows_AndResolvesAnAddress()
    {
        var context = await CreateContextAsync();
        var district = "Dist" + NewCode("D");
        var zoneId = await CreateZoneAsync(context, ZoneBody(context.BranchId, district: district, postalCodes: ["1212"]));

        var detail = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/delivery-zones/{zoneId}")));
        Assert.Equal("BDT", detail.GetProperty("currencyCode").GetString());
        Assert.Equal(context.BranchId, detail.GetProperty("branchId").GetGuid());
        Assert.Equal("1212", detail.GetProperty("coverage")[0].GetProperty("postalCodes")[0].GetString());

        var services = detail.GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(["Standard", "Express"], services.Select(s => s.GetProperty("serviceLevel").GetString()));
        var standard = services[0];
        Assert.Equal("60.00", standard.GetProperty("baseCharge").GetProperty("amount").GetString());
        Assert.Equal("2000.00", standard.GetProperty("freeOverOrderValue").GetProperty("amount").GetString());
        Assert.Equal(3, standard.GetProperty("weightBands").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, standard.GetProperty("weightBands")[2].GetProperty("upToGrams").ValueKind);
        Assert.Equal("10:00–13:00", standard.GetProperty("slotWindows")[0].GetProperty("label").GetString());

        // resolve: a matching address, the same district with another postal code, and an address outside every zone
        var hit = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/delivery-zones/resolve?district={district}&postalCode=1212")));
        Assert.True(hit.GetProperty("matched").GetBoolean());
        Assert.Equal(zoneId, hit.GetProperty("zoneId").GetGuid());
        Assert.Equal(context.BranchId, hit.GetProperty("branchId").GetGuid());

        var miss = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/delivery-zones/resolve?district={district}&postalCode=9999")));
        Assert.False(miss.GetProperty("matched").GetBoolean());
        Assert.Equal("AddressOutsideServedZones", miss.GetProperty("reasonCode").GetString());

        var elsewhere = await ReadAsync(await context.Admin.GetAsync("/api/admin/delivery-zones/resolve?district=Nowhere" + NewCode("N")));
        Assert.False(elsewhere.GetProperty("matched").GetBoolean());
    }

    [Fact]
    public async Task I_B1_ADuplicateZoneName_Is400DeliveryZoneNameAlreadyExists_OnCreateAndUpdate()
    {
        var context = await CreateContextAsync();
        var name = "Dup " + Guid.NewGuid().ToString("N")[..10];
        await CreateZoneAsync(context, ZoneBody(context.BranchId, name));

        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", ZoneBody(context.BranchId, name), Json), "DeliveryZoneNameAlreadyExists");

        var other = await CreateZoneAsync(context, ZoneBody(context.BranchId));
        await AssertErrorAsync(await context.Admin.PutAsJsonAsync($"/api/admin/delivery-zones/{other}", ZoneBody(context.BranchId, name), Json), "DeliveryZoneNameAlreadyExists");

        // keeping its own name is not a duplicate
        var own = await ReadAsync(await context.Admin.GetAsync($"/api/admin/delivery-zones/{other}"));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync(
            $"/api/admin/delivery-zones/{other}", ZoneBody(context.BranchId, own.GetProperty("name").GetString(), priority: 9), Json));
        Assert.Equal(9, (await ReadAsync(await context.Admin.GetAsync($"/api/admin/delivery-zones/{other}"))).GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task AZoneWithABadShape_Is400_NotAnUnhandledError()
    {
        var context = await CreateContextAsync();

        // an unknown service level, a bad time and a negative charge are shape (validator); overlapping windows are the aggregate's
        await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", new
        {
            branchId = context.BranchId, name = "Bad " + NewCode("Z"), coverage = new[] { new { district = "Dhaka", postalCodes = Array.Empty<string>() } },
            services = new[] { new { serviceLevel = "Teleport", isActive = true, baseCharge = 60m, leadTimeDays = 1 } },
        }, Json));

        await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", new
        {
            branchId = context.BranchId, name = "Bad " + NewCode("Z"), coverage = new[] { new { district = "Dhaka", postalCodes = Array.Empty<string>() } },
            services = new[] { new { serviceLevel = "Standard", isActive = true, baseCharge = -1m, leadTimeDays = 1 } },
        }, Json));

        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", new
        {
            branchId = context.BranchId, name = "Bad " + NewCode("Z"), coverage = new[] { new { district = "Dhaka", postalCodes = Array.Empty<string>() } },
            services = new[]
            {
                new
                {
                    serviceLevel = "Standard", isActive = true, baseCharge = 60m, leadTimeDays = 1,
                    slotWindows = new[] { new { dayOfWeek = "Monday", start = "10:00", end = "13:00" }, new { dayOfWeek = "Monday", start = "12:00", end = "15:00" } },
                },
            },
        }, Json), "InvalidDeliveryZone");

        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", ZoneBody(Guid.NewGuid()), Json), "BranchNotFound");
    }

    [Fact]
    public async Task AZone_CanBeDeactivated_AndThenStopsResolving()
    {
        var context = await CreateContextAsync();
        var district = "Dist" + NewCode("D");
        var zoneId = await CreateZoneAsync(context, ZoneBody(context.BranchId, district: district));

        Assert.True((await ReadAsync(await context.Admin.GetAsync($"/api/admin/delivery-zones/resolve?district={district}"))).GetProperty("matched").GetBoolean());

        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync($"/api/admin/delivery-zones/{zoneId}/activation", new { isActive = false }, Json));
        Assert.False((await ReadAsync(await context.Admin.GetAsync($"/api/admin/delivery-zones/resolve?district={district}"))).GetProperty("matched").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.GetAsync($"/api/admin/delivery-zones/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.PutAsJsonAsync($"/api/admin/delivery-zones/{Guid.NewGuid()}/activation", new { isActive = true }, Json)).StatusCode);
    }

    // ── I-B2 ────────────────────────────────────────────────────────────────────────────────────────
    private static object DiscountBody(string code, string kind = "Percentage", decimal? percent = 10m, decimal? amount = null, string scope = "Order", int? usageLimit = null,
        Guid[]? productIds = null, int[]? categoryIds = null) => new
    {
        code, description = "Test code", kind, percent = kind == "Percentage" ? percent : null, amount, minimumSpend = (decimal?)null, scope,
        productIds = productIds ?? [], categoryIds = categoryIds ?? [], firstOrderOnly = false, usageLimit, perCustomerLimit = (int?)null,
        validFrom = (DateTimeOffset?)null, validTo = (DateTimeOffset?)null, isActive = true,
    };


    [Fact]
    public async Task I_B2_AShopManagerManagesDiscountCodes_AnInventoryOfficerCannot()
    {
        var context = await CreateContextAsync();
        var (manager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        var (inventory, _) = await StaffClientAsync(context, StaffRoles.Inventory);

        var code = NewDiscountCode();
        var create = await ExpectAsync(HttpStatusCode.Created, await manager.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(code), Json));
        var id = (await ReadAsync(create)).GetProperty("discountCodeId").GetGuid();

        // the inventory officer is refused on every discount endpoint, read and write
        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.GetAsync("/api/admin/discount-codes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.PutAsJsonAsync($"/api/admin/discount-codes/{id}/activation", new { isActive = false }, Json)).StatusCode);

        // the list carries the code (upper-case) and its redemption counter
        var list = (await ReadAsync(await manager.GetAsync("/api/admin/discount-codes"))).EnumerateArray().First(c => c.GetProperty("id").GetGuid() == id);
        Assert.Equal(code.ToUpperInvariant(), list.GetProperty("code").GetString());
        Assert.Equal(0, list.GetProperty("timesRedeemed").GetInt32());
        Assert.Equal("Percentage", list.GetProperty("kind").GetString());
        Assert.Equal(10m, list.GetProperty("percent").GetDecimal());

        // edit (not the code), deactivate
        await ExpectAsync(HttpStatusCode.NoContent, await manager.PutAsJsonAsync($"/api/admin/discount-codes/{id}", new
        {
            description = "Edited", kind = "FixedAmount", amount = 150.5m, scope = "Order", productIds = Array.Empty<Guid>(), categoryIds = Array.Empty<int>(),
            firstOrderOnly = true, usageLimit = 3, isActive = true,
        }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await manager.PutAsJsonAsync($"/api/admin/discount-codes/{id}/activation", new { isActive = false }, Json));

        var edited = (await ReadAsync(await manager.GetAsync("/api/admin/discount-codes"))).EnumerateArray().First(c => c.GetProperty("id").GetGuid() == id);
        Assert.Equal("FixedAmount", edited.GetProperty("kind").GetString());
        Assert.Equal("150.50", edited.GetProperty("amount").GetProperty("amount").GetString());
        Assert.True(edited.GetProperty("firstOrderOnly").GetBoolean());
        Assert.False(edited.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, edited.GetProperty("usageLimit").GetInt32());
    }

    [Fact]
    public async Task ADiscountCode_IsUnique_CaseInsensitively_AndRefusesBadDefinitions()
    {
        var context = await CreateContextAsync();
        var code = NewDiscountCode();
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(code), Json));

        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(code.ToUpperInvariant()), Json), "DiscountCodeAlreadyExists");

        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode(), percent: 120m), Json), "InvalidDiscountDefinition");
        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode(), scope: "Products"), Json), "InvalidDiscountDefinition");
        await AssertErrorAsync(await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode(), scope: "Products", productIds: [Guid.NewGuid()]), Json), "InvalidDiscountDefinition");
        await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode(), kind: "Mystery"), Json));
        await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody("has space"), Json));
    }

    [Fact]
    public async Task ACategoryScopedCode_AndAProductScopedCode_AreAcceptedWhenTheyNameRealThings()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context);

        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes",
            DiscountBody(NewDiscountCode(), scope: "Products", productIds: [sku.ProductId]), Json));
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes",
            DiscountBody(NewDiscountCode(), scope: "Categories", categoryIds: [2]), Json));
    }

    [Fact]
    public async Task ADiscountCodeValidityWindow_WithAnOffset_IsStoredInUtc()
    {
        var context = await CreateContextAsync();
        var code = NewDiscountCode();
        var body = new
        {
            code, kind = "Percentage", percent = 10m, scope = "Order", productIds = Array.Empty<Guid>(), categoryIds = Array.Empty<int>(),
            validFrom = "2027-01-01T10:00:00+06:00", validTo = "2027-02-01T10:00:00+06:00", isActive = true,
        };

        var response = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", body, Json));
        var id = (await ReadAsync(response)).GetProperty("discountCodeId").GetGuid();

        var stored = await WithDbAsync(db => db.DiscountCodes.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.ValidFrom, c.ValidTo }).SingleAsync());
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 4, 0, 0, TimeSpan.Zero), stored.ValidFrom);
        Assert.Equal(TimeSpan.Zero, stored.ValidTo!.Value.Offset);
    }

    // ── couriers and the bank account ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task ACourier_IsCreatedEditedAndUniqueByName_AndAMalformedTemplateIs400()
    {
        var context = await CreateContextAsync();
        var (manager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        // The list is name-ordered and capped, and the shared database keeps every courier other tests made: a name that sorts first stays on the page.
        var name = "0000 Courier " + Guid.NewGuid().ToString("N")[..10];

        var created = await ExpectAsync(HttpStatusCode.Created, await manager.PostAsJsonAsync("/api/admin/couriers",
            new { name, trackingUrlTemplate = "https://track.example.test/t/{tracking}", supportsCod = true, isActive = true }, Json));
        var id = (await ReadAsync(created)).GetProperty("courierId").GetGuid();

        await AssertErrorAsync(await manager.PostAsJsonAsync("/api/admin/couriers", new { name, supportsCod = false, isActive = true }, Json), "CourierNameAlreadyExists");
        await AssertErrorAsync(await manager.PostAsJsonAsync("/api/admin/couriers",
            new { name = name + "x", trackingUrlTemplate = "http://insecure.example.test/{tracking}", isActive = true }, Json), "InvalidCourier");

        await ExpectAsync(HttpStatusCode.NoContent, await manager.PutAsJsonAsync($"/api/admin/couriers/{id}",
            new { name, trackingUrlTemplate = (string?)null, supportsCod = false, isActive = false }, Json));

        var listed = (await ReadAsync(await manager.GetAsync("/api/admin/couriers"))).EnumerateArray().First(c => c.GetProperty("id").GetGuid() == id);
        Assert.False(listed.GetProperty("isActive").GetBoolean());
        Assert.False(listed.GetProperty("supportsCod").GetBoolean());
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("trackingUrlTemplate").ValueKind);

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/api/admin/couriers/{Guid.NewGuid()}", new { name = "x", isActive = true }, Json)).StatusCode);
    }

    [Fact]
    public async Task TheBankTransferAccount_IsAdministratorOnly_AndASingleton()
    {
        var context = await CreateContextAsync();
        var (manager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        var body = new { accountName = "Purr Care Ltd", accountNumber = "1234567890", bankName = "Sample Bank", branchName = "Gulshan", routingNumber = "012345678", isActive = true };

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync("/api/admin/bank-transfer-account", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/admin/bank-transfer-account")).StatusCode);

        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync("/api/admin/bank-transfer-account", body, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync("/api/admin/bank-transfer-account", body with { accountName = "Renamed Ltd" }, Json));

        var read = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/admin/bank-transfer-account")));
        Assert.True(read.GetProperty("configured").GetBoolean());
        Assert.Equal("Renamed Ltd", read.GetProperty("accountName").GetString());
        Assert.Equal(1, await WithDbAsync(db => db.BankTransferAccounts.AsNoTracking().CountAsync()));
        Assert.Equal("no-store", (await context.Admin.GetAsync("/api/admin/bank-transfer-account")).Headers.CacheControl?.ToString());

        await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.PutAsJsonAsync("/api/admin/bank-transfer-account", body with { accountNumber = " " }, Json));
    }

    // ── §12.3: who may configure ────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Nurse")]
    [InlineData("Vet")]
    [InlineData("Caretaker")]
    [InlineData("Inventory")]
    public async Task TheConfigurationEndpoints_RefuseRolesAppendixDDoesNotGiveThem(string role)
    {
        var context = await CreateContextAsync();
        var (client, _) = await StaffClientAsync(context, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/delivery-zones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/delivery-zones", ZoneBody(context.BranchId), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/couriers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/discount-codes")).StatusCode);
    }

    [Fact]
    public async Task AnAccountant_CanViewZonesAndCodes_ButNotManageThem()
    {
        var context = await CreateContextAsync();
        var (accountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);

        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/admin/delivery-zones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/admin/discount-codes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync("/api/admin/delivery-zones", ZoneBody(context.BranchId), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync("/api/admin/discount-codes", DiscountBody(NewDiscountCode()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/admin/couriers")).StatusCode);
    }

    [Fact]
    public async Task TheConfigurationEndpoints_Refuse_AnAnonymousCaller()
    {
        var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/delivery-zones")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/discount-codes")).StatusCode);
    }
}
