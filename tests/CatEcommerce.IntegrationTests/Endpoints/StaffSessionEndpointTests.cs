using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Staff sign-in and idle (G).
///
/// Gate fix 2026-09-26 (HO-ADM-001 CODE_REVIEW seventh pass, HIGH, on HO-ACC-001's behalf):
/// <see cref="PassiveGet_IdleExpired_AsTheFirstRequestAfterIdling_ReturnsSessionIdleTimeoutNotIdentity"/>
/// hard-asserts the ambient <c>security.staffIdleTimeoutMinutes</c> effective value is the registry
/// default (30), with no capture/restore of its own -- the exact same class of race
/// <c>AdminGlobalSettingsLiveEffectEndpointTests</c>'s own isolated collection exists to prevent,
/// against the identical global, shared-database setting its own I-D4 test changes to 5. Joining
/// <see cref="AdministrationGlobalSettingsCollection"/> (serialized with, never concurrent to, every
/// global-settings-mutating test) closes that gap without needing to touch the assertion itself.</summary>
[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class StaffSessionEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task<Guid> SeedStaffWithoutPasswordAsync(string mobile)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();

        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "New Hire", MobileNumber.Create(mobile), null, ["Nurse"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        return staff.Id;
    }

    // I-G1
    [Fact]
    public async Task NewStaffMember_SetsPasswordViaReset_ThenSignsIn()
    {
        using var client = CreateClient();
        var mobile = NewMobile();
        var staffAccountId = await SeedStaffWithoutPasswordAsync(mobile);

        var requestReset = await client.PostAsJsonAsync("/api/staff/auth/password-reset/request", new { identifier = mobile }, Json);
        Assert.Equal(HttpStatusCode.OK, requestReset.StatusCode);

        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(staffAccountId);
        Assert.NotNull(dispatched);

        const string password = "brandnewstaffpw1";
        var confirm = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched!.PlainToken, newPassword = password }, Json);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var status = await client.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var body = await status.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.TryGetProperty("roleIds", out _));
    }

    // I-G3 / I-G4 / I-G5
    [Fact]
    public async Task IdleBeyondTimeout_RefusesForeverAfter_NoSideEffectAndAuditedExactlyOnce()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Idle Tester", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        // security.staffIdleTimeoutMinutes = 30 (DB setting, seed default -- HO-ADM-001;
        // AccountOptions.StaffIdleTimeoutMinutes now only seeds the registry) -- advance well past it.
        timeProvider.Advance(TimeSpan.FromMinutes(31));

        // I-G4: the refused request is itself a walk-in registration -- must have no side effect.
        var walkInMobile = NewMobile();
        var refusedWalkIn = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Should Not Exist", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refusedWalkIn.StatusCode);
        Assert.Contains("session_idle_timeout", await refusedWalkIn.Content.ReadAsStringAsync());

        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var normalisedWalkInMobile = "+880" + walkInMobile[1..];
            var exists = await db.CustomerAccounts.AsNoTracking().AnyAsync(c => c.MobileNumber!.Value == normalisedWalkInMobile);
            Assert.False(exists);
        }

        // I-G3: forever after -- another request still refuses with the same error.
        var again = await client.GetAsync("/api/staff/session");
        // GetStatus is [PassiveSession] and does not dispatch the heartbeat, so it reads the
        // session row directly: once ended, GetStaffSessionStatusQuery finds no active row.
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);

        // I-G5: three concurrent refused requests (all past idle) -> exactly one audit row.
        var heartbeatMobile = NewMobile();
        _ = heartbeatMobile; // not used further -- concurrency below reuses the same expired session

        var concurrentAttempts = await Task.WhenAll(
            client.PostAsync("/api/staff/session/activity", null),
            client.PostAsync("/api/staff/session/activity", null),
            client.PostAsync("/api/staff/session/activity", null));
        Assert.All(concurrentAttempts, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));

        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var idleExpiredRows = await db.AuditEntries.AsNoTracking()
                .CountAsync(e => e.ActorUserId == staff.Id && e.Action == "StaffSessionIdleExpired");
            Assert.Equal(1, idleExpiredRows);
        }
    }

    // I-G6
    [Fact]
    public async Task GetSession_Repeatedly_DoesNotAdvanceLastActivityOn()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Passive Reader", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        using var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var first = await client.GetAsync("/api/staff/session");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        var firstActivity = firstBody.GetProperty("lastActivityOn").GetDateTimeOffset();

        var second = await client.GetAsync("/api/staff/session");
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        var secondActivity = secondBody.GetProperty("lastActivityOn").GetDateTimeOffset();

        Assert.Equal(firstActivity, secondActivity);
    }

    // ARCHITECTURE gate (MEDIUM): [PassiveSession] must apply SessionStateRules.Classify, so an
    // idle-expired session's FIRST post-idle request being the passive GET does not return 200
    // with identity (R5) -- before this fix, SessionAuthenticationMiddleware's isPassive branch
    // returned ResolvedCaller.Staff(...) unconditionally, and this controller action returned it
    // straight back as a 200 body.
    [Fact]
    public async Task PassiveGet_IdleExpired_AsTheFirstRequestAfterIdling_ReturnsSessionIdleTimeoutNotIdentity()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Passive Idle Tester", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // security.staffIdleTimeoutMinutes = 30 (DB setting, seed default -- HO-ADM-001;
        // AccountOptions.StaffIdleTimeoutMinutes now only seeds the registry) -- advance well past
        // it, then go STRAIGHT to the passive GET, with no intervening non-passive request that would
        // otherwise end the session first (that scenario is I-G3, already covered).
        timeProvider.Advance(TimeSpan.FromMinutes(31));

        var status = await client.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);

        var body = await status.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("session_idle_timeout", body.GetProperty("error").GetString());
        Assert.Equal(30, body.GetProperty("idleTimeoutMinutes").GetInt32());

        // No identity anywhere in the refusal (R5).
        var raw = await status.Content.ReadAsStringAsync();
        Assert.DoesNotContain(staff.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roleIds", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullName", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ARCHITECTURE gate (second pass, LOW): the passive path must not just REFUSE an idle-expired
    // session -- it must also make it terminal (EndForIdleTimeout) and record the idle-expiry
    // audit exactly once, ADR-037's "made terminal on detection, audited once". Before this fix,
    // GET /api/staff/session never dispatched anything on the passive path, so the session row
    // stayed un-ended and StaffSessionIdleExpiredEvent was never audited until some later,
    // non-passive request happened to occur -- which might never happen once the cookie is gone.
    [Fact]
    public async Task PassiveGet_IdleExpired_EndsTheSessionAndAuditsExactlyOnce()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Passive Idle Terminator Tester", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        timeProvider.Advance(TimeSpan.FromMinutes(31));

        // The passive GET is the ONLY request made after idling -- if this doesn't end the
        // session itself, nothing else in this test ever will.
        var status = await client.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);

        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var session = await db.StaffSessions.AsNoTracking().SingleAsync(s => s.StaffAccountId == staff.Id);
        Assert.NotNull(session.EndedOn);
        Assert.Equal(SessionEndReason.IdleTimeout, session.EndReason);

        var idleExpiredAuditRows = await db.AuditEntries.AsNoTracking()
            .CountAsync(e => e.ActorUserId == staff.Id && e.Action == "StaffSessionIdleExpired");
        Assert.Equal(1, idleExpiredAuditRows);
    }

    // ARCHITECTURE gate (MEDIUM): the absolute-expiry sibling of the test above -- Q-G1's own SQL
    // filter (`ExpiresOn > asOf`) already excludes an absolutely-expired row, so this is a
    // regression guard confirming that path still refuses cleanly with no identity, unaffected by
    // the [PassiveSession] Classify fix.
    [Fact]
    public async Task PassiveGet_AbsoluteExpired_ReturnsSessionInvalidNotIdentity()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Passive Absolute-Expiry Tester", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // AccountOptions.StaffSessionLifetimeHours = 12 (appsettings.json) -- advance well past
        // the absolute lifetime, straight to the passive GET.
        timeProvider.Advance(TimeSpan.FromHours(13));

        var status = await client.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);

        var body = await status.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("session_invalid", body.GetProperty("error").GetString());

        var raw = await status.Content.ReadAsStringAsync();
        Assert.DoesNotContain(staff.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roleIds", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullName", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ARCHITECTURE gate fifth pass, MEDIUM (2026-09-25): [CsrfExempt] skips session resolution
    // ENTIRELY for sign-in, not only the CSRF check (HO-ACC-001 §1 Amended (18)'s correction). One
    // consequence: presenting an idle-expired staff session cookie directly to sign-in, with no
    // intervening non-exempt request, must NOT end that old session via EndForIdleTimeout or audit
    // it at that moment -- the row stays not-ended until it is next presented to a non-exempt
    // endpoint, exactly like an abandoned or cleared cookie already is (ADR-037's "audited once, on
    // detection", only the detection moment moves). The gate asked TEST to confirm this directly.
    [Fact]
    public async Task SignInWithAnIdleExpiredCookie_NeitherEndsNorAuditsTheOldSession_UntilItIsNextPresentedToANonExemptEndpoint()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = false // the OLD cookie must be replayed by hand after re-sign-in overwrites it
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Csrf Exempt Idle Tester", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var firstSignIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, firstSignIn.StatusCode);
        var oldSessionToken = ExtractCookieValue(firstSignIn, "pc_staff_session");

        // security.staffIdleTimeoutMinutes = 30 (DB setting, seed default -- HO-ADM-001;
        // AccountOptions.StaffIdleTimeoutMinutes now only seeds the registry) -- advance well past
        // it, with NOTHING else touching this session in between.
        timeProvider.Advance(TimeSpan.FromMinutes(31));

        // Re-sign-in, presenting the OLD, now idle-expired cookie -- [CsrfExempt] means this must
        // succeed regardless, and must not itself end or audit the old session.
        var reSignIn = new HttpRequestMessage(HttpMethod.Post, "/api/staff/auth/sign-in")
        {
            Content = JsonContent.Create(new { identifier = mobile, password }, options: Json)
        };
        reSignIn.Headers.Add("Cookie", $"pc_staff_session={oldSessionToken}");
        var reSignInResponse = await client.SendAsync(reSignIn);
        Assert.Equal(HttpStatusCode.OK, reSignInResponse.StatusCode);

        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var oldSession = await db.StaffSessions.AsNoTracking()
                .Where(s => s.StaffAccountId == staff.Id)
                .OrderBy(s => s.IssuedOn)
                .FirstAsync();

            Assert.Null(oldSession.EndedOn);

            var idleExpiredRowsBeforeNextPresentation = await db.AuditEntries.AsNoTracking()
                .CountAsync(e => e.ActorUserId == staff.Id && e.Action == "StaffSessionIdleExpired");
            Assert.Equal(0, idleExpiredRowsBeforeNextPresentation);
        }

        // Presenting the OLD cookie to a non-exempt, [PassiveSession] endpoint is what finally
        // makes it terminal and audits it -- the detection moment moves, but ADR-037's "audited
        // once, on detection" still holds.
        var oldSessionCheck = new HttpRequestMessage(HttpMethod.Get, "/api/staff/session");
        oldSessionCheck.Headers.Add("Cookie", $"pc_staff_session={oldSessionToken}");
        var oldSessionCheckResponse = await client.SendAsync(oldSessionCheck);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSessionCheckResponse.StatusCode);
        var oldSessionCheckBody = await oldSessionCheckResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("session_idle_timeout", oldSessionCheckBody.GetProperty("error").GetString());

        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var oldSession = await db.StaffSessions.AsNoTracking()
                .Where(s => s.StaffAccountId == staff.Id)
                .OrderBy(s => s.IssuedOn)
                .FirstAsync();

            Assert.NotNull(oldSession.EndedOn);
            Assert.Equal(SessionEndReason.IdleTimeout, oldSession.EndReason);

            var idleExpiredRowsAfterNextPresentation = await db.AuditEntries.AsNoTracking()
                .CountAsync(e => e.ActorUserId == staff.Id && e.Action == "StaffSessionIdleExpired");
            Assert.Equal(1, idleExpiredRowsAfterNextPresentation);
        }
    }

    // I-G7
    [Fact]
    public async Task SignInAgainAfterIdle_NewSessionWorks_SameStaffAccountId()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Returns After Idle", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var firstSignIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, firstSignIn.StatusCode);

        timeProvider.Advance(TimeSpan.FromMinutes(31));

        // GET /api/staff/session is [PassiveSession] and never dispatches the heartbeat, so it
        // cannot itself detect/transition an idle session -- the heartbeat endpoint is what
        // actually evaluates and ends it (matching I-G3/I-G4's use of a non-passive endpoint).
        var idleCheck = await client.PostAsync("/api/staff/session/activity", null);
        Assert.Equal(HttpStatusCode.Unauthorized, idleCheck.StatusCode);

        var secondSignIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, secondSignIn.StatusCode);
        var secondSignInBody = await secondSignIn.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(staff.Id, secondSignInBody.GetProperty("staffAccountId").GetGuid());

        var status = await client.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }
}
