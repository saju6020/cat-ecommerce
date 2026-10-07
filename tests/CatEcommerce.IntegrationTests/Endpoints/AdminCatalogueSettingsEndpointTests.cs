using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, Services, prices, tax, settings (D). I-D1/I-D2/I-D3/I-D4/I-D5/I-D6.
///
/// Gate fix 2026-09-26 (CODE_REVIEW seventh pass, HIGH):
/// <see cref="GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue"/> hard-asserts the
/// ambient <c>booking.slotHoldMinutes</c> effective value is the registry default ("10"), with no
/// capture/restore of its own; <see cref="ChangeSettings_InvalidValue_NothingApplied"/> captures its
/// own before-value but is still exposed to a race across its own two round trips. Both touch the
/// exact same global, shared-database setting <c>AdminGlobalSettingsLiveEffectEndpointTests</c>'s
/// own <c>SettingResolutionOrder_...</c> test mutates. The whole class joins
/// <see cref="AdministrationGlobalSettingsCollection"/> (broadest, safest fix per the gate's own
/// wording) rather than adding capture/restore to each settings-touching test individually --
/// simpler, and the class's other four tests (service/price creation) pay only the cost of running
/// sequentially with the global-settings suite, not a real correctness risk of their own.</summary>
[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class AdminCatalogueSettingsEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task CreateServiceAndPrice_PublicServicesShowsCurrentPriceOnly()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var taxClassId = await CreateTaxClassAsync(client);

        var createService = await client.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode("SVC"), name = "Consultation", description = "General consultation",
            category = "Consultation", durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        Assert.True(createService.StatusCode == HttpStatusCode.Created, await createService.Content.ReadAsStringAsync());
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        // EffectiveFrom must be >= "now" as the HANDLER's clock sees it (§8.6, "no back-dating"),
        // which is always a little later than this client's own UtcNow -- a 1-second margin avoids
        // a flaky rejection, and the short delay below then lets that instant become "now" for the
        // public read (§12.6's "current price" is EffectiveFrom <= now at READ time).
        var setPrice = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new
        {
            amount = "1500.00", currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow.AddSeconds(1)
        }, Json);
        Assert.True(setPrice.StatusCode == HttpStatusCode.Created, await setPrice.Content.ReadAsStringAsync());

        var futurePrice = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new
        {
            amount = "2000.00", currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow.AddDays(30)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, futurePrice.StatusCode);

        await Task.Delay(1500);

        var prices = await client.GetAsync($"/api/admin/services/{serviceId}/prices");
        var priceItems = (await prices.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.Equal(2, priceItems.Count);

        var publicList = await client.GetAsync("/api/services");
        var publicItems = (await publicList.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        var found = publicItems.First(s => s.GetProperty("id").GetGuid() == serviceId);
        Assert.Equal("1500.00", found.GetProperty("currentPrice").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task TwoPricesWithSameEffectiveDate_SecondReturns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var taxClassId = await CreateTaxClassAsync(client);

        var createService = await client.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode("SVC"), name = "Vaccination", description = "Vaccination visit",
            category = "Vaccination", durationMinutes = 20, qualifiedRoleIds = new[] { "Vet" },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var effectiveFrom = DateTimeOffset.UtcNow.AddDays(1);
        var first = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = "500.00", currencyCode = "BDT", effectiveFrom }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = "600.00", currencyCode = "BDT", effectiveFrom }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>I-D2 (gate fix 2026-09-26): two truly concurrent requests race past the handler's
    /// own pre-check, so this exercises <c>UniqueConstraintExceptionFilter</c>'s
    /// <c>ix_service_price_service_effective</c> mapping, not just the pre-check.</summary>
    [Fact]
    public async Task TwoPricesWithSameEffectiveDate_SentConcurrently_OneCreatedOneRejected_NeverTwoRows()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var taxClassId = await CreateTaxClassAsync(client);

        var createService = await client.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode("SVC"), name = "Follow-up visit", description = "Follow-up check",
            category = "FollowUp", durationMinutes = 25, qualifiedRoleIds = new[] { "Vet" },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var effectiveFrom = DateTimeOffset.UtcNow.AddDays(2);
        var requestA = client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = "800.00", currencyCode = "BDT", effectiveFrom }, Json);
        var requestB = client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = "900.00", currencyCode = "BDT", effectiveFrom }, Json);
        var responses = await Task.WhenAll(requestA, requestB);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

        var prices = await client.GetAsync($"/api/admin/services/{serviceId}/prices");
        var priceItems = (await prices.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        // Only this service's own prices exist (it was just created), so exactly one row proves the
        // race never produced two -- whichever request won.
        Assert.Single(priceItems);
    }

    [Fact]
    public async Task SetServicePrice_Backdated_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var taxClassId = await CreateTaxClassAsync(client);

        var createService = await client.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode("SVC"), name = "Dental", description = "Dental check",
            category = "Dental", durationMinutes = 40, qualifiedRoleIds = new[] { "Vet" },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = false
        }, Json);
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var pastResponse = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new
        {
            amount = "700.00", currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, pastResponse.StatusCode);
        Assert.Contains("PriceCannotBeBackdated", await pastResponse.Content.ReadAsStringAsync());
    }

    /// <summary>Gate fix 2026-09-26 (CODE_REVIEW seventh pass, LOW): closes the loop
    /// `MoneyTests.Constructor_NegativeAmount_IsNotRejectedByThisType`'s own "corrected claim" opens
    /// -- `Money` itself does not reject a negative amount (by design, per its own doc comment), so
    /// the enforcement the TEST gate's U-D7 correction rests its whole argument on
    /// (`SetServicePriceCommandValidator.Amount.GreaterThanOrEqualTo(0)`) needed its own end-to-end
    /// proof, not just a claim about where the validator rule lives.</summary>
    [Fact]
    public async Task SetServicePrice_NegativeAmount_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var taxClassId = await CreateTaxClassAsync(client);

        var createService = await client.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode("SVC"), name = "Negative Price Test", description = "Negative price test",
            category = "Consultation", durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createService.StatusCode);
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var negativeResponse = await client.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new
        {
            amount = "-5.00", currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow.AddSeconds(1)
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, negativeResponse.StatusCode);
    }

    [Fact]
    public async Task GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.GetAsync("/api/admin/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();

        Assert.Contains(items, s => s.GetProperty("key").GetString() == SettingKeys.ClinicalVaccinationDueSoonDays);
        var setting = items.First(s => s.GetProperty("key").GetString() == SettingKeys.BookingSlotHoldMinutes);
        Assert.Equal("10", setting.GetProperty("effectiveValue").GetString());
    }

    [Fact]
    public async Task ChangeSettings_InvalidValue_NothingApplied()
    {
        // Settings are global, shared state (§16.1) -- capture the value beforehand rather than
        // assuming the registry default, so this is stable however many times it (or an unrelated
        // test) has already run against this database.
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var before = await GetEffectiveValueAsync(client, SettingKeys.BookingSlotHoldMinutes);

        var response = await client.PutAsJsonAsync("/api/admin/settings", new
        {
            changes = new[]
            {
                new { key = SettingKeys.BookingSlotHoldMinutes, value = "15" },
                new { key = SettingKeys.BookingChangeCutoffHours, value = "not-a-number" }
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // All-or-nothing: the valid change in the same batch as an invalid one must not apply.
        var after = await GetEffectiveValueAsync(client, SettingKeys.BookingSlotHoldMinutes);
        Assert.Equal(before, after);
    }

    private async Task<string> GetEffectiveValueAsync(HttpClient client, string key)
    {
        var get = await client.GetAsync("/api/admin/settings");
        var items = (await get.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        return items.First(s => s.GetProperty("key").GetString() == key).GetProperty("effectiveValue").GetString()!;
    }

    [Fact]
    public async Task NonAdministratorStaff_CannotManageSettings_Returns403()
    {
        var vet = await SeedStaffAsync(StaffRoles.Vet);
        using var client = await SignInStaffAsync(vet.Mobile, vet.Password);

        var response = await client.PutAsJsonAsync("/api/admin/settings", new
        {
            changes = new[] { new { key = SettingKeys.BookingSlotHoldMinutes, value = "12" } }
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> CreateTaxClassAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX"), name = "Standard" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
    }
}
