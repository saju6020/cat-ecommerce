using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ACC-001 ARCHITECTURE gate fourth pass, question 1 / SECURITY gate third pass, assessment 1
/// (2026-09-25): a session row written before the <c>AddSessionCsrfTokenHash</c> migration ran gets
/// <c>csrf_token_hash = ''</c> from that migration's own additive default -- no header value can
/// ever hash to the empty string (SHA-256 hex is always 64 characters), so such a row must fail
/// closed on every unsafe request until a safe (GET) request opportunistically re-issues a fresh
/// token, per ADR-342's named recovery path. The SECURITY gate verified this with an ad hoc probe
/// (<c>UPDATE ... SET csrf_token_hash = ''</c>), not a committed test -- TEST is named as the gate
/// that owes the permanent regression case. This test reproduces the same simulated-legacy-row setup
/// directly against a real session row and the real middleware.
/// </summary>
public sealed class LegacyCsrfTokenHashTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task SimulateLegacyRowAsync(Guid customerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE account.customer_session SET csrf_token_hash = '' WHERE customer_account_id = {customerId}");
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task LegacyEmptyHashSession_RealPreviouslyIssuedCsrfHeader_IsStillRejected()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var realCsrf = ExtractCookieValue(signIn, "pc_csrf");

        await SimulateLegacyRowAsync(customerId);

        // The header carries the ONE value that was genuinely issued for this session -- if the
        // check ever compared "is there a header" rather than "does its hash match the stored
        // value", this would wrongly succeed.
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me")
        {
            Content = JsonContent.Create(new { fullName = "Legacy Row Attempt", additionalPhoneNumbers = Array.Empty<string>() }, options: Json)
        };
        request.Headers.Add("X-CSRF-Token", realCsrf);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("csrf_token_invalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LegacyEmptyHashSession_EmptyHeader_IsStillRejectedNotWronglyAccepted()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        await SimulateLegacyRowAsync(customerId);

        // An empty header against an empty stored hash must NOT be treated as a match -- the
        // middleware refuses a null/empty header before ever comparing hashes (§ SessionAuthentication
        // Middleware.ShouldRefuseUnsafeRequest: IsNullOrEmpty(presented) short-circuits to refuse).
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me")
        {
            Content = JsonContent.Create(new { fullName = "Empty Header Attempt", additionalPhoneNumbers = Array.Empty<string>() }, options: Json)
        };
        request.Headers.Add("X-CSRF-Token", "");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LegacyEmptyHashSession_RecoversAfterASafeRequestReissuesAFreshToken()
    {
        using var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        await SimulateLegacyRowAsync(customerId);

        // ADR-342's named recovery path: a safe (GET) request opportunistically re-issues pc_csrf
        // once it notices the caller's own copy no longer matches the stored hash.
        var getResponse = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var freshCsrf = ExtractCookieValue(getResponse, "pc_csrf");
        Assert.NotEmpty(freshCsrf);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me")
        {
            Content = JsonContent.Create(new { fullName = "Recovered", additionalPhoneNumbers = Array.Empty<string>() }, options: Json)
        };
        request.Headers.Add("X-CSRF-Token", freshCsrf);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
