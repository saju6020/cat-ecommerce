using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ACC-001 §16, Foundation & audit (A) -- US-ACC-12 AC2.
///
/// ACCEPTANCE gate, HIGH (A28, 2026-09-25): the content assertions below were added because the
/// original U-A8 only checked that a DataChange row exists, which let a wrong before/after
/// implementation through three gates. Every content assertion here was proven to fail against the
/// pre-fix <c>AuditChangeCaptureInterceptor</c> (before/after built from the same "Added or
/// Modified only" entry set, with Added entries reading CurrentValue for "before" too) by
/// temporarily reverting the interceptor and re-running this file -- see the round's handback
/// report for the exact failure output. No temporary revert is committed.
///
/// OWNER DECISION (2026-09-25): personal data in DataChange before/after values is recorded in
/// full (FR-ACC-12 as written) -- email, mobile and address are asserted here as their real
/// values, not masked, because that is what the interceptor is now required to do.
/// </summary>
public sealed class AuditImmutabilityTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    // I-A4 -- the database is the authoritative layer; "anyone" includes a raw SQL client.
    [Fact]
    public async Task UpdateOnAuditEntry_ThroughRawSql_Raises()
    {
        using var client = CreateClient();
        var (customerId, _, _) = await RegisterCustomerAsync(client);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        // EF Core's SqlQuery<T> for a scalar type requires the result column aliased as "Value".
        var entryId = await db.Database.SqlQuery<Guid>(
                $"SELECT \"Id\" AS \"Value\" FROM audit.audit_entry WHERE \"EntityId\" = {customerId} LIMIT 1")
            .FirstAsync();

        var exception = await Record.ExceptionAsync(() =>
            db.Database.ExecuteSqlAsync($"UPDATE audit.audit_entry SET \"Reason\" = 'tampered' WHERE \"Id\" = {entryId}"));

        Assert.NotNull(exception);
        Assert.Contains("immutable", exception!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteOnAuditEntry_ThroughRawSql_Raises()
    {
        using var client = CreateClient();
        var (customerId, _, _) = await RegisterCustomerAsync(client);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var exception = await Record.ExceptionAsync(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM audit.audit_entry WHERE \"EntityId\" = {customerId}"));

        Assert.NotNull(exception);
        Assert.Contains("immutable", exception!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // I-A6, strengthened (ACCEPTANCE gate LOW): the fixed-IP factory is what makes the IP
    // assertion meaningful -- TestServer supplies no remote address otherwise, so
    // HttpContextClientIpAccessor would always report null and this assertion would be vacuous.
    // Also registers a cat through the same request pipeline, proving the interceptor's IP
    // capture and insert-shaped before/after both hold for a bounded context other than Accounts
    // (the three shipped AuditCat*Handlers HO-ACC-001 §8.11 updates for Category/IP).
    [Fact]
    public async Task Registration_ProducesADataChangeAuditRowInTheSameTransactionWithIp()
    {
        const string ip = "203.0.113.21";
        var fixedIpFactory = Factory.WithFixedRemoteIpAddress(ip);
        using var client = fixedIpFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/") });

        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var customerRow = await GetLatestAuditRowAsync(fixedIpFactory, customerId, "CustomerRegisteredEvent");

        Assert.Null(customerRow.Before);
        Assert.NotNull(customerRow.After);
        Assert.Contains("FullName", customerRow.After);
        Assert.Equal(IPAddress.Parse(ip), customerRow.IpAddress);

        // HO-ADM-001 §12.3 / ADR-043: Cats.Register now requires a signed-in caller.
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var catResponse = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = customerId, name = "Audit Test Cat", sex = "Female", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, catResponse.StatusCode);
        var catBody = await catResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var catId = catBody.GetProperty("catId").GetGuid();

        var catRow = await GetLatestAuditRowAsync(fixedIpFactory, catId, "CatRegisteredEvent");

        Assert.Null(catRow.Before);
        Assert.NotNull(catRow.After);
        Assert.Equal(IPAddress.Parse(ip), catRow.IpAddress);
    }

    // ACCEPTANCE gate HIGH (A28): a modified CatAggregate scalar field. This case was already
    // correct before the fix (root scalar changes never went through the buggy Added-owned path),
    // but it is asserted here so the whole before/after contract for this file is exercised in one
    // place, not just the parts that were broken.
    [Fact]
    public async Task CatProfileUpdate_ProducesABeforeAfterPairWithTheOldAndNewName()
    {
        // HO-ADM-001 §12.3 / ADR-043: Cats.Register/EditProfile now require a signed-in caller --
        // a customer works here and their forced OwnerId (F19) is exactly what this test needs.
        var (client, ownerId, _, _) = await RegisterAndSignInAsync();
        using var _client = client;

        var registerResponse = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId, name = "Original Cat Name", sex = "Male", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var catId = (await registerResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("catId").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/profile", new
        {
            name = "Renamed Cat", sex = "Male", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, catId, "CatProfileUpdatedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.Contains("\"Name\":\"Original Cat Name\"", row.Before);
        Assert.DoesNotContain("Original Cat Name", row.After);
        Assert.Contains("\"Name\":\"Renamed Cat\"", row.After);
    }

    // ACCEPTANCE gate HIGH (A28): setting an email from null. Before the fix, the newly-Added
    // EmailAddress owned entry appeared in BOTH before and after (CollectProperties read
    // CurrentValue for an Added entry regardless of which snapshot it was building) -- an account
    // that never had an email would show one in "before". OWNER DECISION 2026-09-25: recorded in
    // full, not masked.
    [Fact]
    public async Task SettingEmail_FromNull_TheNewAddressAppearsOnlyInAfter()
    {
        var (client, customerId, _, password) = await RegisterAndSignInAsync();
        var newEmail = NewEmail();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Adding An Email", emailAddress = newEmail, additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = password
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "CustomerContactDetailsUpdatedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.DoesNotContain(newEmail, row.Before);
        Assert.Contains(newEmail, row.After);
    }

    // ACCEPTANCE gate HIGH (A28): an email CHANGE, old value -> new value. This is the exact
    // account-takeover-relevant case the finding called out: before the fix, the old (Deleted)
    // EmailAddress owned entry was dropped entirely (only Added/Modified entries were captured),
    // so the new email appeared in BOTH before and after and the row could not say what the
    // email used to be. OWNER DECISION 2026-09-25: both the old and new email are recorded in
    // full, not masked.
    [Fact]
    public async Task ChangingEmail_OldToNew_BeforeHasTheOldValueAfterHasTheNew()
    {
        var (client, customerId, _, password) = await RegisterAndSignInAsync();
        var firstEmail = NewEmail();

        var firstSet = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Adding An Email", emailAddress = firstEmail, additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = password
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, firstSet.StatusCode);

        var secondEmail = NewEmail();
        var changeResponse = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Changing The Email", emailAddress = secondEmail, additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = password
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "CustomerContactDetailsUpdatedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.Contains(firstEmail, row.Before);
        Assert.DoesNotContain(secondEmail, row.Before);
        Assert.Contains(secondEmail, row.After);
        Assert.DoesNotContain(firstEmail, row.After);
    }

    // ACCEPTANCE gate HIGH (A28): an address ADDED to an EXISTING address book (the book's root
    // aggregate is Modified, not Added, so this exercises the owned-COLLECTION case, distinct
    // from the whole-book-created-on-first-write insert case already covered by I-A6's cat/
    // customer inserts).
    [Fact]
    public async Task AddingASecondAddress_TheNewAddressAppearsOnlyInAfter()
    {
        var (client, customerId, _, _) = await RegisterAndSignInAsync();

        var first = await client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Home", line1 = "111 First Street", city = "Dhaka", district = "Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Work", line1 = "222 Second Street", city = "Dhaka", district = "Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "DeliveryAddressAddedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.DoesNotContain("222 Second Street", row.Before);
        Assert.Contains("222 Second Street", row.After);
    }

    // ACCEPTANCE gate HIGH (A28): an address REMOVED. Before the fix, a Deleted owned entry never
    // reached either snapshot at all (the initial query kept only Added/Modified), so the removed
    // address disappeared from the audit trail entirely instead of appearing in "before".
    [Fact]
    public async Task RemovingAnAddress_TheRemovedAddressAppearsInBeforeAndNotInAfter()
    {
        var (client, customerId, _, _) = await RegisterAndSignInAsync();

        var first = await client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Home", line1 = "111 First Street", city = "Dhaka", district = "Dhaka"
        }, Json);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        _ = firstBody;

        var second = await client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Work", line1 = "222 Second Street", city = "Dhaka", district = "Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondAddressId = (await second.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("addressId").GetGuid();

        // The second address is not the default (the first one became default on creation), so
        // removing it does not hit DefaultAddressCannotBeRemoved.
        var remove = await client.DeleteAsync($"/api/customers/me/addresses/{secondAddressId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "DeliveryAddressRemovedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.Contains("222 Second Street", row.Before);
        Assert.DoesNotContain("222 Second Street", row.After);
    }

    // ACCEPTANCE gate HIGH (A28): a failed sign-in's LoginThrottle.FailedCount, 0 -> 1. This is
    // the exact regression the gate observed live: replacing an owned VALUE OBJECT (not a
    // collection member) is tracked by EF as a Deleted-old plus Added-new pair, so before the fix
    // every failed-sign-in row showed "1 -> 1" (the Added entry's CurrentValue used for both
    // snapshots) and the lockout counter's own change was never recorded.
    [Fact]
    public async Task FailedSignIn_RecordsFailedCountGoingFromZeroToOne()
    {
        using var client = CreateClient();
        var (customerId, mobile, _) = await RegisterCustomerAsync(client);

        var attempt = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "definitely-the-wrong-password" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "CustomerSignInFailedEvent");

        Assert.NotNull(row.Before);
        Assert.NotNull(row.After);
        Assert.Contains("\"LoginThrottle.FailedCount\":0", row.Before);
        Assert.Contains("\"LoginThrottle.FailedCount\":1", row.After);
    }

    // ACCEPTANCE gate: xmin (the EF/Postgres concurrency token) must never appear in a captured
    // snapshot -- it is not business data, and its value changes on every write regardless of
    // whether anything meaningful changed.
    [Fact]
    public async Task NoAuditRow_EverMentionsXmin()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        await client.PutAsJsonAsync("/api/customers/me", new { fullName = "Concurrency Token Check", additionalPhoneNumbers = Array.Empty<string>() }, Json);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var rows = await db.AuditEntries.AsNoTracking()
            .Where(e => e.EntityId == customerId)
            .Select(e => new { e.BeforeValue, e.AfterValue })
            .ToListAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.DoesNotContain("xmin", r.BeforeValue ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("xmin", r.AfterValue ?? "", StringComparison.OrdinalIgnoreCase);
        });
    }

    // ACCEPTANCE gate: secrets (the password hash, the security stamp) are redacted in every
    // snapshot that carries them -- registration is the simplest case that always has one.
    [Fact]
    public async Task Registration_RedactsThePasswordCredentialAndSecurityStamp()
    {
        using var client = CreateClient();
        var (customerId, _, _) = await RegisterCustomerAsync(client);

        var row = await GetLatestAuditRowAsync(Factory, customerId, "CustomerRegisteredEvent");

        Assert.NotNull(row.After);
        Assert.Contains("\"PasswordCredential.Hash\":\"[redacted]\"", row.After);
        Assert.Contains("\"SecurityStamp\":\"[redacted]\"", row.After);
    }

    // I-A7 (ACCEPTANCE gate LOW): the audit IP reflects a FORWARDED address only when the
    // immediate peer is a trusted proxy -- proves the positive half live, distinct from the
    // rotating-XFF rate-limit test, which never asserts anything about the audit IP.
    [Fact]
    public async Task AuditIp_WithATrustedProxy_ReflectsTheForwardedAddressNotTheProxys()
    {
        const string proxyIp = "203.0.113.9";
        const string forwardedClientIp = "198.51.100.7";
        var trustedProxyFactory = Factory.WithFixedRemoteIpAddress(proxyIp, trustAsProxy: true);
        using var client = trustedProxyFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/") });

        var (captchaId, answer) = await GetCaptchaAsync(client);
        var mobile = NewMobile();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new
            {
                fullName = "Forwarded Ip Customer", mobileNumber = mobile, password = "correcthorsebattery",
                captchaId, captchaAnswer = answer
            }, options: Json)
        };
        request.Headers.Add("X-Forwarded-For", forwardedClientIp);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customerId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        var row = await GetLatestAuditRowAsync(trustedProxyFactory, customerId, "CustomerRegisteredEvent");

        Assert.Equal(IPAddress.Parse(forwardedClientIp), row.IpAddress);
    }

    // I-A7's negative half: the same header, from the same peer, is ignored when that peer is
    // NOT a trusted proxy -- the audit IP is the raw connection address, never the header value.
    [Fact]
    public async Task AuditIp_WithoutATrustedProxy_IgnoresTheForwardedHeader()
    {
        const string peerIp = "203.0.113.10";
        const string forwardedClientIp = "198.51.100.8";
        var untrustedFactory = Factory.WithFixedRemoteIpAddress(peerIp, trustAsProxy: false);
        using var client = untrustedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/") });

        var (captchaId, answer) = await GetCaptchaAsync(client);
        var mobile = NewMobile();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new
            {
                fullName = "Untrusted Proxy Customer", mobileNumber = mobile, password = "correcthorsebattery",
                captchaId, captchaAnswer = answer
            }, options: Json)
        };
        request.Headers.Add("X-Forwarded-For", forwardedClientIp);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customerId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        var row = await GetLatestAuditRowAsync(untrustedFactory, customerId, "CustomerRegisteredEvent");

        Assert.Equal(IPAddress.Parse(peerIp), row.IpAddress);
    }

    private static async Task<AuditRow> GetLatestAuditRowAsync(WebApplicationFactory<Program> hostFactory, Guid entityId, string action)
    {
        await using var scope = hostFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var row = await db.AuditEntries.AsNoTracking()
            .Where(e => e.EntityId == entityId && e.Action == action && e.Category == AuditCategory.DataChange)
            .OrderByDescending(e => e.OccurredOn)
            .FirstAsync();

        return new AuditRow(row.BeforeValue, row.AfterValue, row.IpAddress);
    }

    private async Task<(HttpClient Client, Guid CustomerId, string Mobile, string Password)> RegisterAndSignInAsync()
    {
        var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, customerId, mobile, password);
    }

    private sealed record AuditRow(string? Before, string? After, IPAddress? IpAddress);
}
