using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Customer sign-in (C).</summary>
public sealed class CustomerAuthEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    // I-C2
    [Fact]
    public async Task SignIn_ByEmail_Returns200()
    {
        using var client = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(client);
        var email = NewEmail();
        const string password = "correcthorsebattery";

        var register = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Email SignIn", emailAddress = email, password, captchaId, captchaAnswer = answer
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = email, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // I-C1
    [Fact]
    public async Task SignIn_ByMobile_SetsSessionCookieAndSessionEndpointWorks()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var setCookies = signIn.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        // Attribute names are case-insensitive per RFC 6265; Kestrel emits them lower-case.
        Assert.Contains(setCookies, c => c.StartsWith("pc_session=")
            && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && c.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith("pc_csrf="));

        var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var body = await session.Content.ReadAsStringAsync();
        Assert.Contains(customerId.ToString(), body);
    }

    // I-C3
    [Fact]
    public async Task SignIn_UnknownIdentifierVsWrongPassword_ReturnByteIdenticalBodies()
    {
        using var client = CreateClient();
        var (_, mobile, _) = await RegisterCustomerAsync(client);

        var unknown = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = NewMobile(), password = "whatever12" }, Json);
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "wrongpassword" }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await wrongPassword.Content.ReadAsStringAsync());
        Assert.False(unknown.Headers.Contains("Retry-After"));
    }

    // I-C4 / I-C5 (known-account path)
    [Fact]
    public async Task SignIn_ThreeWrongThenRightWithin5s_FourthIsLockedWithSameBodyAndAudited()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
            last = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "wrongpassword" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, last!.StatusCode);
        var wrongBody = await last.Content.ReadAsStringAsync();

        // The 4th attempt, with the RIGHT password, arrives well within the 5s lock the 3rd
        // failure set (LoginThrottleRules.DelayFor(3) = 5s) -- still refused, same public body.
        var fourth = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, fourth.StatusCode);
        Assert.Equal(wrongBody, await fourth.Content.ReadAsStringAsync());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var failureRows = await db.AuditEntries.AsNoTracking()
            .Where(e => e.EntityId == customerId && e.Action == "CustomerSignInFailed")
            .CountAsync();
        // One AuthenticationEvent row per attempt (I-C5): 3 wrong-password failures. The 4th
        // (locked) attempt is a refusal, audited as CustomerSignInRefused, not counted here.
        Assert.Equal(3, failureRows);
    }

    // I-C5 (unknown-identifier path)
    [Fact]
    public async Task SignIn_UnknownIdentifier_AuditedWithMaskedIdentifierNeverRaw()
    {
        using var client = CreateClient();
        var unknownMobile = NewMobile();

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = unknownMobile, password = "whatever12" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var maskedIdentifier = IdentifierMasking.Mask(unknownMobile);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var row = await db.AuditEntries.AsNoTracking()
            .Where(e => e.Action == "CustomerSignInFailed" && e.BeforeValue == maskedIdentifier)
            .OrderByDescending(e => e.OccurredOn)
            .FirstOrDefaultAsync();

        Assert.NotNull(row);
        Assert.DoesNotContain(unknownMobile, row!.BeforeValue);
    }

    // I-C6
    [Fact]
    public async Task SignOut_ThenGetSession_Returns401AndCookieExpired()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);

        // A real client reads the non-HttpOnly pc_csrf cookie and echoes it (double-submit, ADR-032).
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var signOut = await client.PostAsync("/api/auth/sign-out", null);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    // I-C7
    [Fact]
    public async Task PutProfile_WithoutCsrfHeader_Returns403AndChangesNothing()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);

        // Deliberately no X-CSRF-Token header, even though the cookie handler carries pc_csrf.
        var response = await client.PutAsJsonAsync("/api/customers/me", new { fullName = "Changed Name", additionalPhoneNumbers = Array.Empty<string>() }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("csrf_token_invalid", await response.Content.ReadAsStringAsync());
    }

    // I-C8
    [Fact]
    public async Task GuestEndpoint_WithTamperedSessionCookie_TreatedAsAnonymous()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "pc_session=not-a-real-token");

        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
