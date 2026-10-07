using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// SECURITY gate item 7 (HO-ACC-002 first-pass gate fix round, owner decision 2026-09-26):
/// <c>pc_csrf</c> needs a configurable <c>Domain</c> attribute (<see cref="AccountsApiFactory.WithCsrfCookieDomain"/>)
/// so a real cross-subdomain production deployment (web app and API on two different subdomains of
/// one registrable domain) can make it readable from the web app's own origin via
/// <c>document.cookie</c> -- a host-only cookie set by the API's own response is otherwise never
/// visible there at all. <c>pc_session</c>/<c>pc_staff_session</c> must stay host-only regardless,
/// since they are <c>HttpOnly</c> and only need the browser to attach them automatically.
/// </summary>
public sealed class CsrfCookieDomainTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public async Task SignIn_WithNoConfiguredCsrfCookieDomain_SetsNeitherCookieWithADomainAttribute()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var sessionCookieHeader = ExtractSetCookieHeader(signIn, "pc_session");
        var csrfCookieHeader = ExtractSetCookieHeader(signIn, "pc_csrf");

        Assert.DoesNotContain("Domain=", sessionCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain=", csrfCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_WithAConfiguredCsrfCookieDomain_SetsItOnlyOnTheCsrfCookie()
    {
        const string configuredDomain = ".example.test";
        using var client = Factory.WithCsrfCookieDomain(configuredDomain).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = false
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var sessionCookieHeader = ExtractSetCookieHeader(signIn, "pc_session");
        var csrfCookieHeader = ExtractSetCookieHeader(signIn, "pc_csrf");

        Assert.DoesNotContain("Domain=", sessionCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Domain={configuredDomain}", csrfCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaffSignIn_WithAConfiguredCsrfCookieDomain_SetsItOnlyOnTheCsrfCookie()
    {
        const string configuredDomain = ".example.test";
        var staffFactory = Factory.WithCsrfCookieDomain(configuredDomain);
        await using var scope = staffFactory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Domain Test Staff", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        using var client = staffFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = false
        });

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var staffSessionCookieHeader = ExtractSetCookieHeader(signIn, "pc_staff_session");
        var csrfCookieHeader = ExtractSetCookieHeader(signIn, "pc_csrf");

        Assert.DoesNotContain("Domain=", staffSessionCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Domain={configuredDomain}", csrfCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    // SECURITY gate item 7 -- CustomerProfileController's account-deletion action used to expire
    // pc_csrf with a bare Cookies.Delete(name), which never carries a configured Domain; the
    // browser then treats that as a different cookie (host-only) from the real, domain-scoped one
    // and never actually removes it. Now routed through SessionCookieWriter.ExpireCsrf, which knows
    // the configured Domain.
    [Fact]
    public async Task DeleteAccount_WithAConfiguredCsrfCookieDomain_ExpiresTheCsrfCookieWithTheSameDomain()
    {
        const string configuredDomain = ".example.test";
        using var client = Factory.WithCsrfCookieDomain(configuredDomain).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = false
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var sessionToken = ExtractCookieValue(signIn, "pc_session");
        var csrfToken = ExtractCookieValue(signIn, "pc_csrf");

        // HandleCookies=false above (a Domain not matching "localhost" makes .NET's own
        // CookieContainer throw when parsing the Set-Cookie header, same as SignIn_With... above)
        // -- cookies for this second request are attached by hand instead, same pattern
        // CorsAndCsrfTests uses for the same reason.
        var deletionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers/me/deletion")
        {
            Content = JsonContent.Create(new { currentPassword = password }, options: Json)
        };
        deletionRequest.Headers.Add("Cookie", $"pc_session={sessionToken}; pc_csrf={csrfToken}");
        deletionRequest.Headers.Add("X-CSRF-Token", csrfToken);

        var deletion = await client.SendAsync(deletionRequest);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        var csrfExpiryHeader = ExtractSetCookieHeader(deletion, "pc_csrf");
        var sessionExpiryHeader = ExtractSetCookieHeader(deletion, "pc_session");

        Assert.Contains($"Domain={configuredDomain}", csrfExpiryHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain=", sessionExpiryHeader, StringComparison.OrdinalIgnoreCase);
    }

    // SECURITY gate optional hardening (second pass) -- AccountOptions.CsrfCookieDomain is now
    // validated at startup (ValidateOnStart, the same mechanism StaffIdleTimeoutMinutes's [Range]
    // already uses), so an operator typo fails the moment the host starts, not as a silently
    // fail-closed 403 once real traffic hits it. Accessing `.Services` is what actually starts the
    // host in a WebApplicationFactory -- the options-validation framework validates every option
    // configured with ValidateOnStart() as part of that startup, with no custom hosted service of
    // this handover's own needed.
    [Theory]
    [InlineData("https://.example.test")] // scheme
    [InlineData(".example.test:5005")] // port
    [InlineData(".example.test/path")] // path
    [InlineData(" .example.test")] // leading whitespace
    [InlineData(".example.test ")] // trailing whitespace
    [InlineData(".example .test")] // internal whitespace
    [InlineData("..example.test")] // empty label
    // CODE_REVIEW gate third pass, LOW -- an IP literal is not a valid Domain value either (RFC
    // 6265 SS5.1.3: a browser compares an IP-address request host against Domain for exact string
    // equality, never subdomain-matching, so "every subdomain" scoping is meaningless for one), and
    // some browsers refuse the cookie outright rather than silently degrading to host-only.
    [InlineData("127.0.0.1")] // IPv4 dotted-quad
    [InlineData(".127.0.0.1")] // IPv4 dotted-quad, with the same leading dot a real domain would use
    [InlineData("192.168.1.1")]
    [InlineData("::1")] // IPv6 loopback
    [InlineData("[::1]")] // IPv6 loopback, the bracketed form a URL authority component would use
    [InlineData("fe80::1")] // IPv6 link-local
    [InlineData("2001:db8::1")] // IPv6 documentation range
    // CODE_REVIEW gate fourth pass, LOW -- the dotted-quad check above only catches the canonical
    // four-octet decimal form; browsers (and some IPv4 parsers) also recognise several
    // shorthand/alternate-base forms as the very same loopback/private address, none of which are a
    // valid DNS TLD either.
    [InlineData("0177.0.0.1")] // octal loopback (0177 octal == 127 decimal)
    [InlineData("0x7f.0.0.1")] // hex loopback (0x7f == 127 decimal)
    [InlineData("0x7f000001")] // bare hex loopback, single label, no dots at all
    [InlineData("127.1")] // shorthand loopback (last label absorbs the missing two octets)
    [InlineData("127.0.1")] // shorthand loopback (last label absorbs the missing octet)
    [InlineData("2130706433")] // bare 32-bit integer loopback
    public void InvalidCsrfCookieDomain_FailsAtStartup(string invalidDomain)
    {
        var invalidFactory = Factory.WithCsrfCookieDomain(invalidDomain);

        var exception = Assert.ThrowsAny<Exception>(() => _ = invalidFactory.Services);

        Assert.Contains("CsrfCookieDomain", exception.ToString());
    }

    [Theory]
    [InlineData("")] // host-only, the default
    [InlineData(".example.test")] // the documented "this domain and every subdomain" form
    [InlineData("example.test")] // a bare hostname, no leading dot, is also a valid Domain value
    [InlineData("a.b.c.example.test")]
    // CODE_REVIEW gate third pass, LOW -- proves the new IP-literal rejection does not over-match: a
    // hostname is free to contain digits, or even start with one, as long as it is not itself
    // exactly a four-part, all-numeric dotted quad.
    [InlineData("example123.test")]
    [InlineData("10.example.test")]
    // CODE_REVIEW gate fourth pass, LOW -- proves the new last-label check does not over-match
    // either: a non-final label starting with "0x" or being all-digit is still just an ordinary
    // hostname label, since only the LAST label is inspected.
    [InlineData("0x123.example.test")]
    public void ValidCsrfCookieDomain_StartsUpSuccessfully(string validDomain)
    {
        var validFactory = Factory.WithCsrfCookieDomain(validDomain);

        var services = validFactory.Services;

        Assert.NotNull(services);
    }
}
