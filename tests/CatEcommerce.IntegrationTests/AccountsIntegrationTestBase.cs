using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests;

public abstract class AccountsIntegrationTestBase(AccountsApiFactory factory) : CatEcommerceIntegrationTestBase<AccountsApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    /// <summary>Mints a collision-resistant test mobile number -- §16.1 rule 1.</summary>
    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewEmail() => $"t-{Guid.NewGuid():N}@example.test";

    protected async Task<(string CaptchaId, string Answer)> GetCaptchaAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/captcha", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (body.GetProperty("captchaId").GetString()!, "TEST1");
    }

    /// <summary>
    /// Extracts a cookie's raw value from a response's Set-Cookie headers (URL-decoded). The CSRF
    /// cookie is deliberately non-HttpOnly so a real client can read it and echo it back in the
    /// X-CSRF-Token header (double-submit, ADR-032); tests using HandleCookies=true must do the same
    /// since the CookieContainer used by the test client is not otherwise inspectable.
    /// </summary>
    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        var rawValue = header[(cookieName.Length + 1)..].Split(';')[0];
        return Uri.UnescapeDataString(rawValue);
    }

    /// <summary>
    /// SECURITY gate item 7 (first-pass gate fix round) -- the raw <c>Set-Cookie</c> header line
    /// itself (attributes included), for asserting on <c>Domain=</c> being present/absent, which
    /// <see cref="ExtractCookieValue"/> deliberately strips away.
    /// </summary>
    protected static string ExtractSetCookieHeader(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        return setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
    }

    protected async Task<(Guid CustomerId, string Mobile, string Password)> RegisterCustomerAsync(HttpClient client, string? mobile = null)
    {
        var (captchaId, answer) = await GetCaptchaAsync(client);
        mobile ??= NewMobile();
        const string password = "correcthorsebattery";

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Integration Test Customer",
            mobileNumber = mobile,
            password,
            captchaId,
            captchaAnswer = answer
        }, Json);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (body.GetProperty("customerId").GetGuid(), mobile, password);
    }
}
