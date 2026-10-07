using System.Net;
using System.Net.Http.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, rate limiting (ADR-030) -- uses the tight-limit factory variant.</summary>
public sealed class RateLimitingTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    // I-B9
    [Fact]
    public async Task Registration_OverLimit_ThirdReturns429WithRetryAfterAndNoIpInBody()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            var captchaResponse = await client.PostAsync("/api/captcha", null);
            var captchaBody = await captchaResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);

            last = await client.PostAsJsonAsync("/api/customers", new
            {
                fullName = "Rate Limited", mobileNumber = NewMobile(), password = "correcthorsebattery",
                captchaId = captchaBody.GetProperty("captchaId").GetString(), captchaAnswer = "TEST1"
            }, Json);
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
        var body = await last.Content.ReadAsStringAsync();
        Assert.DoesNotContain("127.0.0.1", body);
    }

    // I-C9
    [Fact]
    public async Task SignIn_OverLimit_ThirdReturns429()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
            last = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = NewMobile(), password = "whatever12" }, Json);

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    // SECURITY gate HIGH: with no TrustedProxies/TrustedNetworks configured (the committed
    // appsettings.json), X-Forwarded-For must not be processed at all, so a rotating value must
    // not let a caller spread requests across many rate-limit partitions. Before the fix,
    // ForwardedHeadersMiddleware's own default (empty KnownProxies/KnownNetworks skips the
    // known-IP check) meant the header WAS honoured from any peer, and 8 rotating requests all
    // returned 401 with no 429 ever appearing.
    [Fact]
    public async Task SignIn_RotatingXForwardedFor_StillHitsTheRateLimit()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
            {
                Content = JsonContent.Create(new { identifier = NewMobile(), password = "whatever12" }, options: Json)
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        Assert.Contains((HttpStatusCode)429, statuses);
    }
}
