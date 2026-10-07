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
/// SECURITY gate HIGH (second pass) / ADR-342 (owner decision A, 2026-09-25). The CSRF value must
/// be durable across a process restart, because sessions themselves are (up to 7 days customer, 12
/// hours staff, both in Postgres). Two independent <see cref="WebApplicationFactory{TEntryPoint}"/>
/// hosts against the SAME database simulate a restart: the old design (a Singleton, process-lifetime
/// HMAC key in <c>CsrfTokenBinder</c>) failed this scenario -- every unsafe request, including
/// sign-out and sign-in itself, returned 403 on the second host. The current design (a per-session
/// random token, hashed and stored on the session row) passes it by construction, because both
/// hosts read and write the same row.
/// </summary>
public sealed class CsrfRestartTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private static HttpClient ClientFor(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = false // cookies are attached by hand below, carried across the two hosts
    });

    private static HttpRequestMessage Request(
        HttpMethod method, string path, string sessionCookieName, string sessionToken, string csrfToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        request.Headers.Add("Cookie", $"{sessionCookieName}={sessionToken}; pc_csrf={csrfToken}");
        request.Headers.Add("X-CSRF-Token", csrfToken);
        return request;
    }

    private async Task<(string Mobile, string Password)> SeedReceptionAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Restart Test Reception", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (mobile, password);
    }

    [Fact]
    public async Task Customer_AfterSimulatedRestart_PutSignOutAndReSignIn_AllSucceedOnTheSecondHost()
    {
        using var clientA = ClientFor(Factory);

        var (_, mobile, password) = await RegisterCustomerAsync(clientA);
        var signIn = await clientA.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var sessionToken = ExtractCookieValue(signIn, "pc_session");
        var csrfToken = ExtractCookieValue(signIn, "pc_csrf");

        // Sanity: works on host A itself, before any restart is simulated.
        var putOnA = Request(HttpMethod.Put, "/api/customers/me", "pc_session", sessionToken, csrfToken,
            new { fullName = "Before Restart", additionalPhoneNumbers = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.NoContent, (await clientA.SendAsync(putOnA)).StatusCode);

        // Simulate a restart: an entirely independent host (its own DI container, its own
        // singletons), same underlying Postgres database, same cookies the client already holds.
        using var clientB = ClientFor(Factory.WithWebHostBuilder(_ => { }));

        var putOnB = Request(HttpMethod.Put, "/api/customers/me", "pc_session", sessionToken, csrfToken,
            new { fullName = "After Restart", additionalPhoneNumbers = Array.Empty<string>() });
        var putResponse = await clientB.SendAsync(putOnB);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var signOutOnB = Request(HttpMethod.Post, "/api/auth/sign-out", "pc_session", sessionToken, csrfToken);
        var signOutResponse = await clientB.SendAsync(signOutOnB);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        // Re-sign-in on host B with the OLD (now ended) session cookie still attached and no CSRF
        // header at all -- must succeed regardless: sign-in is [CsrfExempt], a stale or ended
        // session there is treated as anonymous, never as a reason to refuse.
        var reSignIn = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { identifier = mobile, password }, options: Json)
        };
        reSignIn.Headers.Add("Cookie", $"pc_session={sessionToken}; pc_csrf={csrfToken}");
        var reSignInResponse = await clientB.SendAsync(reSignIn);
        Assert.Equal(HttpStatusCode.OK, reSignInResponse.StatusCode);
    }

    [Fact]
    public async Task Staff_AfterSimulatedRestart_WalkInSignOutAndReSignIn_AllSucceedOnTheSecondHost()
    {
        using var clientA = ClientFor(Factory);
        var (mobile, password) = await SeedReceptionAsync();

        var signIn = await clientA.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var sessionToken = ExtractCookieValue(signIn, "pc_staff_session");
        var csrfToken = ExtractCookieValue(signIn, "pc_csrf");

        using var clientB = ClientFor(Factory.WithWebHostBuilder(_ => { }));

        var walkInOnB = Request(HttpMethod.Post, "/api/staff/customers", "pc_staff_session", sessionToken, csrfToken,
            new { fullName = "Restart Walk-in", mobileNumber = NewMobile() });
        var walkInResponse = await clientB.SendAsync(walkInOnB);
        Assert.Equal(HttpStatusCode.Created, walkInResponse.StatusCode);

        var signOutOnB = Request(HttpMethod.Post, "/api/staff/auth/sign-out", "pc_staff_session", sessionToken, csrfToken);
        var signOutResponse = await clientB.SendAsync(signOutOnB);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        var reSignIn = new HttpRequestMessage(HttpMethod.Post, "/api/staff/auth/sign-in")
        {
            Content = JsonContent.Create(new { identifier = mobile, password }, options: Json)
        };
        reSignIn.Headers.Add("Cookie", $"pc_staff_session={sessionToken}; pc_csrf={csrfToken}");
        var reSignInResponse = await clientB.SendAsync(reSignIn);
        Assert.Equal(HttpStatusCode.OK, reSignInResponse.StatusCode);
    }
}
