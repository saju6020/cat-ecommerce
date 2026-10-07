using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// SECURITY gate MEDIUM: the CORS policy must be an explicit, configuration-driven origin
/// allow-list (not the platform's own <c>SetIsOriginAllowed(_ =&gt; true).AllowCredentials()</c>),
/// and the CSRF double-submit value must be bound to the session it was issued for, so a cookie
/// planted from a same-site sibling origin cannot pass the check just by also being echoed in the
/// header.
/// </summary>
public sealed class CorsAndCsrfTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private static HttpRequestMessage Preflight(string origin) =>
        new(HttpMethod.Options, "/api/customers/me")
        {
            Headers =
            {
                { "Origin", origin },
                { "Access-Control-Request-Method", "PUT" },
                { "Access-Control-Request-Headers", "content-type,x-csrf-token" }
            }
        };

    [Fact]
    public async Task Preflight_FromAnUnconfiguredOrigin_GetsNoCorsGrant()
    {
        using var client = CreateClient();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"),
            "An origin not in Cors:AllowedOrigins must never receive an Access-Control-Allow-Origin grant.");
    }

    [Fact]
    public async Task Preflight_FromAConfiguredOrigin_GetsTheCorsGrant()
    {
        const string allowedOrigin = "https://app.example.test";
        using var client = Factory.WithAllowedCorsOrigin(allowedOrigin).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

        var response = await client.SendAsync(Preflight(allowedOrigin));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var originValues));
        Assert.Contains(allowedOrigin, originValues!);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentialsValues));
        Assert.Contains("true", credentialsValues!);
    }

    // Backlog APT row W1: the web app (a separate origin) reads `Date` for the hold countdown's
    // server-clock offset, which a browser only allows when the response exposes it.
    [Fact]
    public async Task CrossOriginRequest_FromAConfiguredOrigin_ExposesTheDateHeader()
    {
        const string allowedOrigin = "https://app.example.test";
        using var client = Factory.WithAllowedCorsOrigin(allowedOrigin).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/branches");
        request.Headers.Add("Origin", allowedOrigin);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed),
            "A cross-origin response must expose Date so the web app can read the server clock.");
        Assert.Contains("Date", string.Join(",", exposed!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_FromAConfiguredOrigin_DoesNotGrantADifferentOrigin()
    {
        using var client = Factory.WithAllowedCorsOrigin("https://app.example.test").CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

        var response = await client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // SECURITY gate MEDIUM: cookie tossing from a same-site sibling origin can plant an arbitrary
    // pc_csrf cookie and echo the SAME value in X-CSRF-Token -- both attacker-controlled, so a
    // naive "cookie equals header" check passes. Binding the value to the session (an HMAC of the
    // session's own token hash) defeats this: the middleware never trusts the request's own
    // pc_csrf cookie value at all, only whether the header matches what it independently
    // recomputes for the REAL, resolved session.
    [Fact]
    public async Task PlantedCsrfCookie_MatchingItsOwnHeader_IsStillRejected()
    {
        // HandleCookies=false: cookies are managed explicitly below (extracted from Set-Cookie,
        // re-attached by hand) so a planted pc_csrf value can be sent instead of the real one --
        // an auto-managed CookieContainer would only ever send back what the server itself issued.
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var sessionToken = ExtractCookieValue(signIn, "pc_session");

        const string plantedValue = "attacker-chosen-value-not-derived-from-any-real-session";

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me")
        {
            Content = JsonContent.Create(new { fullName = "Attacker Attempt", additionalPhoneNumbers = Array.Empty<string>() }, options: Json)
        };
        request.Headers.Add("Cookie", $"pc_session={sessionToken}; pc_csrf={plantedValue}");
        request.Headers.Add("X-CSRF-Token", plantedValue);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("csrf_token_invalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RealCsrfCookie_EchoedInTheHeader_IsAccepted()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var sessionToken = ExtractCookieValue(signIn, "pc_session");
        var realCsrf = ExtractCookieValue(signIn, "pc_csrf");

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me")
        {
            Content = JsonContent.Create(new { fullName = "Legitimate Update", additionalPhoneNumbers = Array.Empty<string>() }, options: Json)
        };
        request.Headers.Add("Cookie", $"pc_session={sessionToken}; pc_csrf={realCsrf}");
        request.Headers.Add("X-CSRF-Token", realCsrf);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
