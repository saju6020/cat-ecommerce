using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Gate fix 2026-09-26 (TEST gate seventh pass, item 1): A11 (I-D3/I-D4) had zero test coverage
/// because <c>clinical.vaccinationDueSoonDays</c> and <c>security.staffIdleTimeoutMinutes</c> are
/// both global, non-branch-overridable, shared state in the one <c>catecommerce_adm</c> database
/// every other integration test in this suite also uses -- a PERMANENT mutation risked a passing
/// value leaking into a concurrently-running, unrelated test class that assumes the registry
/// default (e.g. a test elsewhere in the suite relying on the default 30-minute idle timeout).
///
/// This collection is marked <see cref="AdministrationGlobalSettingsCollection"/>
/// (<c>DisableParallelization = true</c>), the standard xUnit v2 mechanism for a collection of
/// tests that must not run concurrently with each other, so any future test added here that
/// mutates a global setting is automatically serialized with these two. Each test additionally: (a)
/// reads the setting's current effective value BEFORE mutating it, rather than assuming the
/// registry default, and (b) restores that exact value in a <c>finally</c> block, so the database is
/// left exactly as this test found it regardless of assertion outcome -- bounding the window during
/// which a changed value is visible to the shortest span possible.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AdministrationGlobalSettingsCollection
{
    public const string Name = "HO-ADM-001 Global Settings (serialized)";
}

[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class AdminGlobalSettingsLiveEffectEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    /// <summary>I-D3: changing <c>clinical.vaccinationDueSoonDays</c> changes a cat's vaccination
    /// status on the very next read, with no restart. Checked against one item in the response's own
    /// <c>vaccines</c> array (this test's own freshly-registered, unique-coded vaccine), not the
    /// aggregated <c>overallStatus</c> -- the vaccine catalogue is itself shared, global data, and
    /// any other already-registered core vaccine this cat has never received would otherwise also
    /// drive <c>overallStatus</c> to <c>Overdue</c> regardless of this test's own setting change.
    /// </summary>
    [Fact]
    public async Task ChangeVaccinationDueSoonDays_CatVaccinationStatus_ReflectsTheNewValueImmediately()
    {
        // HO-CAT-001 gate fix, SECURITY MEDIUM (2026-09-26): AdministeredByVetId must now resolve
        // to a staff account holding the Vet role, so this admin also holds Vet -- it both signs
        // in to change the global setting (Administrator) and is named as the administering vet.
        var admin = await SeedStaffAsync(StaffRoles.Administrator, StaffRoles.Vet);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var originalValue = await GetEffectiveSettingValueAsync(client, SettingKeys.ClinicalVaccinationDueSoonDays);
        try
        {
            var catId = await SeedCatAsync(Guid.NewGuid());

            var vaccineCode = "V" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var registerVaccine = await client.PostAsJsonAsync("/api/vaccines", new
            {
                code = vaccineCode, name = "Due-Soon Live-Effect Test Vaccine", isCore = true, validityMonths = 12
            }, Json);
            Assert.Equal(HttpStatusCode.Created, registerVaccine.StatusCode);
            var vaccineId = (await registerVaccine.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("vaccineId").GetGuid();

            // NextDueOn = AdministeredOn.AddMonths(12) (CatAggregate.RecordVaccination) -- administered
            // a year and 20 days ago, so NextDueOn lands ~20 days from today.
            var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1).AddDays(20);
            var recordVaccination = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
            {
                vaccineId,
                batchReference = "BATCH-" + Guid.NewGuid().ToString("N")[..6],
                batchExpiresOn = administeredOn.AddYears(2),
                administeredOn,
                administeredByVetId = admin.StaffAccountId
            }, Json);
            Assert.Equal(HttpStatusCode.Created, recordVaccination.StatusCode);

            // A window comfortably wider than the ~20-day gap -> DueSoon.
            await ChangeSettingAsync(client, SettingKeys.ClinicalVaccinationDueSoonDays, "25");
            var wideWindowItem = await GetVaccineItemAsync(client, catId, vaccineId);
            Assert.Equal("DueSoon", wideWindowItem.GetProperty("status").GetString());

            // Same vaccination record, same query -- only the global setting changed in between, no
            // restart -- a window narrower than the gap -> Current.
            await ChangeSettingAsync(client, SettingKeys.ClinicalVaccinationDueSoonDays, "5");
            var narrowWindowItem = await GetVaccineItemAsync(client, catId, vaccineId);
            Assert.Equal("Current", narrowWindowItem.GetProperty("status").GetString());
        }
        finally
        {
            await ChangeSettingAsync(client, SettingKeys.ClinicalVaccinationDueSoonDays, originalValue);
        }
    }

    /// <summary>I-D4: changing <c>security.staffIdleTimeoutMinutes</c> changes when a session times
    /// out, with no restart. Uses a controllable <see cref="FakeTimeProvider"/> (the same technique
    /// <c>AccountsApiFactory.WithFakeTimeProvider()</c> already uses for its own idle-timeout tests)
    /// so idling 6 minutes takes no real wall-clock time.</summary>
    [Fact]
    public async Task ChangeStaffIdleTimeoutMinutes_IdleSession_TimesOutAtTheNewValue()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var originalValue = await GetEffectiveSettingValueAsync(client, SettingKeys.SecurityStaffIdleTimeoutMinutes);
        try
        {
            await ChangeSettingAsync(client, SettingKeys.SecurityStaffIdleTimeoutMinutes, "5");

            timeProvider.Advance(TimeSpan.FromMinutes(6));

            var status = await client.GetAsync("/api/staff/session");
            Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);

            var body = await status.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal("session_idle_timeout", body.GetProperty("error").GetString());
            // The refusal itself reports the EFFECTIVE (changed) timeout, not the registry default
            // (30) -- this is the direct proof the new value is what actually governed the timeout.
            Assert.Equal(5, body.GetProperty("idleTimeoutMinutes").GetInt32());
        }
        finally
        {
            // The idled-out session's cookie no longer authenticates -- a fresh sign-in (same
            // account, same fake clock, just at its now-advanced position) is needed to restore the
            // setting.
            using var restoreClient = await SignInStaffAsync(admin.Mobile, admin.Password);
            await ChangeSettingAsync(restoreClient, SettingKeys.SecurityStaffIdleTimeoutMinutes, originalValue);
        }
    }

    private static async Task<string> GetEffectiveSettingValueAsync(HttpClient client, string key)
    {
        var get = await client.GetAsync("/api/admin/settings");
        var items = (await get.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        return items.First(s => s.GetProperty("key").GetString() == key).GetProperty("effectiveValue").GetString()!;
    }

    private static async Task ChangeSettingAsync(HttpClient client, string key, string value)
    {
        var response = await client.PutAsJsonAsync("/api/admin/settings", new { changes = new[] { new { key, value } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<JsonElement> GetVaccineItemAsync(HttpClient client, Guid catId, Guid vaccineId)
    {
        var response = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("vaccines").EnumerateArray().First(v => v.GetProperty("vaccineId").GetGuid() == vaccineId);
    }

    /// <summary>U-D5: resolution order is branch override -&gt; global value -&gt; the definition's
    /// own default, each winning in turn (<c>CachedBusinessSettings.Resolve</c> / the read side's
    /// identically-shaped <c>GetSettingsQueryHandler</c>). Uses <c>SettingKeys.BookingSlotHoldMinutes</c>
    /// (branch-overridable, bounds 2-30, registry default 10): the registry <c>default</c> the
    /// response itself reports is a fixed constant, unaffected by any database mutation, so setting a
    /// global value distinct from it and observing <c>effectiveValue</c> follow is a direct proof of
    /// "global overrides default", not an assumption about ambient state; setting a branch override
    /// distinct from both, then clearing it, directly proves "branch overrides global, falls back to
    /// global once cleared" against the same live database.</summary>
    [Fact]
    public async Task SettingResolutionOrder_BranchOverride_ThenGlobal_ThenDefault_EachWinsInTurn()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        const string key = SettingKeys.BookingSlotHoldMinutes;
        const string globalValue = "18";
        const string branchValue = "25";

        var originalGlobal = await GetEffectiveSettingValueAsync(client, key);
        try
        {
            var branchCreate = await client.PostAsJsonAsync("/api/admin/branches", new
            {
                code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Settings Resolution Test Branch", timeZoneId = "Asia/Dhaka"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, branchCreate.StatusCode);
            var branchId = (await branchCreate.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

            // A fresh branch has no override for any key -- its own effective value is exactly the
            // (still-ambient) global value, and the registry's own default is reported unchanged.
            var beforeAnyChange = await GetSettingRowAsync(client, key, branchId);
            Assert.Equal("10", beforeAnyChange.GetProperty("default").GetString());

            // (1) Global overrides default: set a global value distinct from the fixed default "10".
            await ChangeSettingAsync(client, key, globalValue);
            var afterGlobalChange = await GetSettingRowAsync(client, key, branchId);
            Assert.Equal("10", afterGlobalChange.GetProperty("default").GetString());
            Assert.Equal(globalValue, afterGlobalChange.GetProperty("globalValue").GetString());
            Assert.Equal(globalValue, afterGlobalChange.GetProperty("effectiveValue").GetString());
            Assert.Null(afterGlobalChange.GetProperty("branchValue").GetString());

            // (2) Branch override wins over global: set an override on this branch, distinct from
            // both the global value and the default.
            var setOverride = await client.PutAsJsonAsync($"/api/admin/settings?branchId={branchId}", new { changes = new[] { new { key, value = branchValue } } }, Json);
            Assert.Equal(HttpStatusCode.NoContent, setOverride.StatusCode);
            var afterBranchOverride = await GetSettingRowAsync(client, key, branchId);
            Assert.Equal(branchValue, afterBranchOverride.GetProperty("branchValue").GetString());
            Assert.Equal(branchValue, afterBranchOverride.GetProperty("effectiveValue").GetString());
            Assert.Equal(globalValue, afterBranchOverride.GetProperty("globalValue").GetString()); // unchanged underneath

            // (3) Clearing the override falls back to global, not default.
            var clearOverride = await client.DeleteAsync($"/api/admin/settings/{key}?branchId={branchId}");
            Assert.Equal(HttpStatusCode.NoContent, clearOverride.StatusCode);
            var afterClear = await GetSettingRowAsync(client, key, branchId);
            Assert.Null(afterClear.GetProperty("branchValue").GetString());
            Assert.Equal(globalValue, afterClear.GetProperty("effectiveValue").GetString());
        }
        finally
        {
            await ChangeSettingAsync(client, key, originalGlobal);
        }
    }

    private static async Task<JsonElement> GetSettingRowAsync(HttpClient client, string key, Guid branchId)
    {
        var response = await client.GetAsync($"/api/admin/settings?branchId={branchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        return items.First(s => s.GetProperty("key").GetString() == key);
    }

    /// <summary>ACCEPTANCE gate first pass, A10/I-D5: a GLOBAL settings change is independently
    /// observed as a <c>DataChange</c> audit row on <c>BusinessSettingsAggregate</c>'s own scope id
    /// (<see cref="SettingScopes.GlobalId"/>), carrying the before and after values -- not inferred
    /// from "<c>UpdateVersion()</c> presumably marks the root Modified", but watched directly by
    /// searching the audit log after the change.</summary>
    [Fact]
    public async Task ChangeGlobalSetting_AuditsADataChangeRowWithBeforeAndAfterValues()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        const string key = SettingKeys.BookingSlotHoldMinutes;
        var originalValue = await GetEffectiveSettingValueAsync(client, key);
        var newValue = originalValue == "20" ? "21" : "20";
        try
        {
            var from = DateTimeOffset.UtcNow.AddMinutes(-5);

            await ChangeSettingAsync(client, key, newValue);

            var to = DateTimeOffset.UtcNow.AddMinutes(5);
            var search = await client.GetAsync(
                $"/api/admin/audit-entries?entityId={SettingScopes.GlobalId}&entityType=BusinessSettingsAggregate&action=BusinessSettingsChangedEvent" +
                $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);

            var items = (await search.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
            Assert.NotEmpty(items);

            var row = items.OrderByDescending(i => i.GetProperty("occurredOn").GetDateTimeOffset()).First();
            var before = row.GetProperty("beforeValue").GetString();
            var after = row.GetProperty("afterValue").GetString();

            Assert.NotNull(after);
            Assert.Contains($"\"{originalValue}\"", before);
            Assert.Contains($"\"{newValue}\"", after);
        }
        finally
        {
            await ChangeSettingAsync(client, key, originalValue);
        }
    }

    /// <summary>ACCEPTANCE gate first pass, A10/I-D5, branch-override half: setting a BRANCH override
    /// (a distinct <c>BusinessSettingsAggregate</c> scoped to the branch id, created on first write)
    /// is also audited as a <c>DataChange</c> row -- here the "before" is the brand-new aggregate's
    /// insert, so only the after value is asserted (matching the interceptor's own documented rule
    /// that an insert has no "before" snapshot).</summary>
    [Fact]
    public async Task ChangeBranchSettingOverride_AuditsADataChangeRowWithTheAfterValue()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var branchCreate = await client.PostAsJsonAsync("/api/admin/branches", new
        {
            code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Settings Audit Test Branch", timeZoneId = "Asia/Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, branchCreate.StatusCode);
        var branchId = (await branchCreate.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        const string key = SettingKeys.BookingSlotHoldMinutes;
        const string overrideValue = "22";

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);

        var setOverride = await client.PutAsJsonAsync($"/api/admin/settings?branchId={branchId}", new { changes = new[] { new { key, value = overrideValue } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setOverride.StatusCode);

        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await client.GetAsync(
            $"/api/admin/audit-entries?entityId={branchId}&entityType=BusinessSettingsAggregate&action=BusinessSettingsChangedEvent" +
            $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var items = (await search.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var row = items.OrderByDescending(i => i.GetProperty("occurredOn").GetDateTimeOffset()).First();
        var after = row.GetProperty("afterValue").GetString();
        Assert.Contains($"\"{overrideValue}\"", after);
    }
}
