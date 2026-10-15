using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 section 16 B -- the erasure ledger end to end. Serialised with the other retention-cycle classes (<c>XctRetention</c>) because an erasure
/// pass processes EVERY due ledger in the shared database, so two classes running passes at once would act on each other's customers.
/// </summary>
[Collection("XctRetention")]
public sealed class XctErasureEndpointTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private const string FixedIp = "203.0.113.9";

    private static async Task RunErasurePassAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RetentionWorkCycle>().RunErasurePassAsync();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private async Task<JsonElement> LedgerAsync(HttpClient admin, Guid customerId) =>
        await ReadAsync(await admin.GetAsync($"/api/admin/retention/erasure-requests/{customerId}"));

    private static JsonElement Scope(JsonElement ledger, string contextKey) =>
        ledger.GetProperty("scopes").EnumerateArray().Single(s => s.GetProperty("contextKey").GetString() == contextKey);

    /// <summary>Publishes a fresh policy that requires acceptance and has <paramref name="customer"/> accept it from <see cref="FixedIp"/>.</summary>
    private async Task AcceptAPolicyAsync(HttpClient admin, HttpClient customer)
    {
        var key = "xct-" + Guid.NewGuid().ToString("N")[..12];
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms", requiresAcceptance = true }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json)).StatusCode);
    }

    private static async Task DeleteOwnAccountAsync(HttpClient customer, string password) =>
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = password }, Json)).StatusCode);

    private async Task<string?> AcceptanceIpAsync(Guid customerId) =>
        await WithDbAsync(db => db.Set<PolicyAcceptanceAggregate>().AsNoTracking().Where(a => a.CustomerAccountId == customerId).Select(a => a.IpAddress).SingleAsync());

    // I-B1
    [Fact]
    public async Task I_B1_ACustomerWhoDeletesTheirAccount_GetsALedgerWithOneScopePerRegisteredEraser_SourceSelfService()
    {
        var withIp = Factory.WithFixedRemoteIpAddress(FixedIp);
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator, withIp);
        var (customer, customerId, _, password) = await RegisterAndSignInCustomerAsync(withIp);

        await DeleteOwnAccountAsync(customer, password);

        var ledger = await LedgerAsync(admin, customerId);
        Assert.Equal("SelfService", ledger.GetProperty("source").GetString());
        Assert.Equal("Open", ledger.GetProperty("status").GetString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var registered = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Select(e => e.ContextKey).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var scopes = ledger.GetProperty("scopes").EnumerateArray().ToList();
        Assert.Equal(registered, scopes.Select(s => s.GetProperty("contextKey").GetString()!).OrderBy(k => k, StringComparer.Ordinal).ToList());
        Assert.Contains("account", registered);
        Assert.Contains("notification", registered);
        Assert.Contains("administration", registered);
        Assert.All(scopes, s => Assert.Equal("Pending", s.GetProperty("status").GetString()));
    }

    // I-B2 + I-B3 + I-B4
    [Fact]
    public async Task I_B2_B3_B4_ThePassErasesWhatItMay_HoldsAcceptanceEvidence_ReleasesItAtTheEndOfThePeriod_AndKeepsClinicalRecords()
    {
        var withIp = Factory.WithFixedRemoteIpAddress(FixedIp);
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator, withIp);
        var (customer, customerId, _, password) = await RegisterAndSignInCustomerAsync(withIp);
        var catId = await SeedCatAsync(customerId, "Held Cat");
        await AcceptAPolicyAsync(admin, customer);
        Assert.Equal(FixedIp, await AcceptanceIpAsync(customerId));

        await DeleteOwnAccountAsync(customer, password);
        await RunErasurePassAsync(Factory);

        // I-B2: account and notification are done; the policy acceptance evidence is held (NFR-LEG-5, six years).
        var ledger = await LedgerAsync(admin, customerId);
        Assert.Equal("Completed", Scope(ledger, "account").GetProperty("status").GetString());
        Assert.Equal("Completed", Scope(ledger, "notification").GetProperty("status").GetString());
        var held = Scope(ledger, "administration");
        Assert.Equal("Held", held.GetProperty("status").GetString());
        Assert.Equal("NFR-LEG-5 acceptance evidence", held.GetProperty("basis").GetString());
        var heldUntil = DateOnly.Parse(held.GetProperty("heldUntil").GetString()!);
        Assert.True(heldUntil > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(5));
        // CODE_REVIEW 15: the hold date is counted on the main branch calendar, the same one as "today" (not the UTC date of the acceptance).
        var acceptedOn = await WithDbAsync(db => db.Set<PolicyAcceptanceAggregate>().AsNoTracking().Where(a => a.CustomerAccountId == customerId).Select(a => a.AcceptedOn).SingleAsync());
        var acceptedLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(acceptedOn, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka")).DateTime);
        Assert.Equal(acceptedLocal.AddYears(6), heldUntil);
        Assert.Equal("Open", ledger.GetProperty("status").GetString());
        Assert.Equal(FixedIp, await AcceptanceIpAsync(customerId));

        // I-B4: the cat (a clinical record) is untouched and its owner now shows the placeholder.
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat WHERE \"Id\" = @id", ("id", catId)));
        Assert.Equal(AnonymisationRules.PlaceholderFullName, await ScalarAsync<string>("SELECT full_name FROM account.customer_account WHERE \"Id\" = @id", ("id", customerId)));

        // I-B3: a clock past the hold -> the same pass releases it; the request completes and the proof point is audited.
        var (afterTheHold, fakeTime) = Factory.WithFakeTimeProvider(DateTimeOffset.UtcNow);
        fakeTime.Advance(TimeSpan.FromDays(366 * 6 + 2));
        await RunErasurePassAsync(afterTheHold);

        var completed = await LedgerAsync(admin, customerId);
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.All(completed.GetProperty("scopes").EnumerateArray(), s => Assert.Equal("Completed", s.GetProperty("status").GetString()));
        Assert.Null(await AcceptanceIpAsync(customerId));
        Assert.Equal(1, await CountAuditAsync("ErasureRequestCompleted", customerId));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat WHERE \"Id\" = @id", ("id", catId)));
    }

    // I-B5 + I-B6 + I-B7
    [Fact]
    public async Task I_B5_B6_B7_AnAdministratorTakesAnErasureRequest_ItIsIdempotent_AndAReceptionistIsRefused()
    {
        var (admin, adminId) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (reception, receptionId) = await SignedInStaffAsync(StaffRoles.Reception);
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var body = new { channel = "Phone", identityCheck = "RegisteredMobileCallback" };

        var first = await ReadAsync(await admin.PostAsJsonAsync($"/api/admin/customers/{customerId}/erasure", body, Json));
        Assert.False(first.GetProperty("isReapplication").GetBoolean());
        Assert.Equal(CustomerAccountStatusName.Anonymised, await ScalarAsync<int>("SELECT status::int FROM account.customer_account WHERE \"Id\" = @id", ("id", customerId)));
        Assert.Equal(1, await CountAuditAsync("CustomerErasureRequestedByStaff", customerId));
        Assert.Equal("StaffOnBehalf", (await LedgerAsync(admin, customerId)).GetProperty("source").GetString());

        var again = await ReadAsync(await admin.PostAsJsonAsync($"/api/admin/customers/{customerId}/erasure", body, Json));
        Assert.True(again.GetProperty("isReapplication").GetBoolean());
        Assert.Equal(1, await CountAuditAsync("CustomerErasureRequestedByStaff", customerId));

        var denied = await reception.PostAsJsonAsync($"/api/admin/customers/{customerId}/erasure", body, Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"Action\" = 'AuthorizationDenied' AND \"ActorUserId\" = @id", ("id", receptionId)) >= 1);
        Assert.NotEqual(adminId, receptionId);
    }

    [Fact]
    public async Task AnAdministratorErasureOfAnUnknownAccount_Is404_AndAnInvalidBodyIs400()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var unknown = await admin.PostAsJsonAsync($"/api/admin/customers/{Guid.NewGuid()}/erasure",
            new { channel = "Letter", identityCheck = "InPersonPhotoId" }, Json);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("CustomerAccountNotFound", await unknown.Content.ReadAsStringAsync());

        var invalid = await admin.PostAsJsonAsync($"/api/admin/customers/{Guid.NewGuid()}/erasure", new { channel = "Carrier pigeon", identityCheck = "", reason = "" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var freeText = await admin.PostAsJsonAsync($"/api/admin/customers/{Guid.NewGuid()}/erasure", new { channel = "Phone", identityCheck = "NID 1990123456, DOB 12/03/85" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, freeText.StatusCode);                  // a fixed list of codes: identifiers can never be typed onto the audit row
    }

    // I-B8
    [Fact]
    public async Task I_B8_WhenTheLedgerHandlerFails_TheRequestFailsAsAccDescribes_AndTheNextPassSelfHeals()
    {
        var failing = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAggregateRootRepository<ErasureRequestAggregate>, ThrowingLedgerRepository>()));
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (customer, customerId, _, password) = await RegisterAndSignInCustomerAsync(failing);

        var deletion = await customer.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = password }, Json);

        Assert.False(deletion.IsSuccessStatusCode);                       // ADR-020: a failing event handler fails the command after commit
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*)::int FROM ops.erasure_request WHERE \"Id\" = @id", ("id", customerId)));
        Assert.Equal(3, await ScalarAsync<int>("SELECT status::int FROM account.customer_account WHERE \"Id\" = @id", ("id", customerId))); // the anonymisation committed

        await RunErasurePassAsync(Factory);                                // self-heal: opens the ledger ...
        await RunErasurePassAsync(Factory);                                // ... and the next unit erases its scopes

        var ledger = await LedgerAsync(admin, customerId);
        Assert.Equal("Completed", Scope(ledger, "account").GetProperty("status").GetString());
    }

    // I-B9
    [Fact]
    public async Task I_B9_ReapplyAfterARestore_ErasesTheActiveAccount_CountsTheRest_AndStampsTheRestoreSource()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (alreadyErased, erasedId, _, erasedPassword) = await RegisterAndSignInCustomerAsync();
        await DeleteOwnAccountAsync(alreadyErased, erasedPassword);
        var (_, activeId, _, _) = await RegisterAndSignInCustomerAsync();

        var result = await ReadAsync(await admin.PostAsJsonAsync("/api/admin/retention/erasures/reapply",
            new { customerAccountIds = new[] { erasedId, activeId, Guid.NewGuid() }, restoreReference = "restore drill 2026-10-12" }, Json));

        Assert.Equal(1, result.GetProperty("processed").GetInt32());
        Assert.Equal(1, result.GetProperty("alreadyErased").GetInt32());
        Assert.Equal(1, result.GetProperty("notFound").GetInt32());
        Assert.Equal(0, result.GetProperty("merged").GetInt32());
        Assert.Equal("RestoreReconciliation", (await LedgerAsync(admin, activeId)).GetProperty("source").GetString());
    }

    [Fact]
    public async Task Reapply_With501Ids_Is400ReapplyListTooLarge()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var response = await admin.PostAsJsonAsync("/api/admin/retention/erasures/reapply",
            new { customerAccountIds = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray(), restoreReference = "r" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ReapplyListTooLarge", await response.Content.ReadAsStringAsync());
    }

    // I-B10
    [Fact]
    public async Task I_B10_TheLedgerRead_ShowsScopesAndBasis_AndNoNameNumberOrEmail()
    {
        var withIp = Factory.WithFixedRemoteIpAddress(FixedIp);
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator, withIp);
        var (customer, customerId, mobile, password) = await RegisterAndSignInCustomerAsync(withIp);
        await AcceptAPolicyAsync(admin, customer);
        await DeleteOwnAccountAsync(customer, password);
        await RunErasurePassAsync(Factory);

        var response = await admin.GetAsync($"/api/admin/retention/erasure-requests/{customerId}");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(mobile, text);
        Assert.DoesNotContain("Test Customer", text);
        Assert.DoesNotContain("@", text);
        var ledger = JsonSerializer.Deserialize<JsonElement>(text, Json);
        var held = Scope(ledger, "administration");
        Assert.Equal("Held", held.GetProperty("status").GetString());
        Assert.True(held.TryGetProperty("heldUntil", out _));
        Assert.True(held.TryGetProperty("basis", out _));
        Assert.True(held.TryGetProperty("attempts", out _));
        Assert.True(held.TryGetProperty("lastErrorCode", out _));

        var list = await ReadAsync(await admin.GetAsync("/api/admin/retention/erasure-requests?status=Open&pageSize=5"));
        Assert.True(list.GetProperty("items").GetArrayLength() <= 5);
    }

    [Fact]
    public async Task TheLedgerReads_AreForRetentionViewers_AccountantsMayRead_ReceptionMayNot()
    {
        var (accountant, _) = await SignedInStaffAsync(StaffRoles.Accountant);
        var (reception, _) = await SignedInStaffAsync(StaffRoles.Reception);

        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/admin/retention/erasure-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/admin/retention/erasure-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await accountant.GetAsync($"/api/admin/retention/erasure-requests/{Guid.NewGuid()}")).StatusCode);
    }

    /// <summary>Fault injection for I-B8: the ledger can be read, but never written.</summary>
    private sealed class ThrowingLedgerRepository : IAggregateRootRepository<ErasureRequestAggregate>
    {
        public Task<ErasureRequestAggregate> GetByIdAsync(Guid id) => Task.FromResult<ErasureRequestAggregate>(null!);
        public Task<ErasureRequestAggregate> GetByFilterAsync(System.Linq.Expressions.Expression<Func<ErasureRequestAggregate, bool>> dataFilters) => Task.FromResult<ErasureRequestAggregate>(null!);
        public Task<bool> ExistAsync(System.Linq.Expressions.Expression<Func<ErasureRequestAggregate, bool>> dataFilters) => Task.FromResult(false);
        public Task SaveAsync(ErasureRequestAggregate aggregate, EventPublishOption eventPublishOption) => throw new InvalidOperationException("injected fault");
        public Task UpdateAsync(ErasureRequestAggregate aggregate, EventPublishOption eventPublishOption) => throw new InvalidOperationException("injected fault");
        public Task UpdateAsync(ErasureRequestAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption) => throw new InvalidOperationException("injected fault");
    }

    private static class CustomerAccountStatusName
    {
        public const int Anonymised = 3;
    }
}
