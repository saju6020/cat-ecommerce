using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Reset (D).</summary>
public sealed class PasswordResetEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    // I-D1
    [Fact]
    public async Task RequestReset_UnknownVsKnown_ReturnsIdenticalBodies()
    {
        using var client = CreateClient();
        var (_, mobile, _) = await RegisterCustomerAsync(client);

        var unknown = await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = NewMobile() }, Json);
        var known = await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
    }

    // I-D2 / I-D3 / I-D4
    [Fact]
    public async Task RequestThenConfirm_Works_AndTokenIsSingleUse()
    {
        using var client = CreateClient();
        var (customerId, mobile, oldPassword) = await RegisterCustomerAsync(client);

        var requestResponse = await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(customerId);
        Assert.NotNull(dispatched);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            var generator = scope.ServiceProvider.GetRequiredService<CatEcommerce.Domain.DomainServices.IPasswordResetTokenGenerator>();
            var expectedHash = generator.ComputeHash(dispatched!.PlainToken);
            var tokenExists = await db.PasswordResetTokens.AsNoTracking().AnyAsync(t => t.TokenHash == expectedHash);
            Assert.True(tokenExists);
        }

        const string newPassword = "brandnewpassword1";
        var confirmResponse = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched!.PlainToken, newPassword }, Json);
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var oldSignIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = oldPassword }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSignIn.StatusCode);

        var newSignIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = newPassword }, Json);
        Assert.Equal(HttpStatusCode.OK, newSignIn.StatusCode);

        // The client now carries a real session cookie from newSignIn, so this next unsafe POST
        // is subject to CSRF double-submit (ADR-032) even though the endpoint itself is anonymous.
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(newSignIn, "pc_csrf"));

        // I-D4: the same token again -> 400 InvalidPasswordResetToken
        var replay = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched.PlainToken, newPassword = "anotherpassword1" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Contains("InvalidPasswordResetToken", await replay.Content.ReadAsStringAsync());
    }

    // I-D6
    [Fact]
    public async Task ExpiredToken_Returns400WithSameMessageAsReplay()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var (customerId, mobile, _) = await RegisterCustomerAsync(client);
        await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);
        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(customerId)!;

        // PasswordResetTokenLifetimeMinutes = 15 (appsettings.json) -- advance well past it.
        timeProvider.Advance(TimeSpan.FromMinutes(16));

        var expired = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched.PlainToken, newPassword = "brandnewpassword1" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Contains("InvalidPasswordResetToken", await expired.Content.ReadAsStringAsync());
    }

    // I-D7
    [Fact]
    public async Task StaffPasswordReset_ConfirmSetsTheStaffAccountsPassword()
    {
        using var client = CreateClient();

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Account.StaffAccountAggregate>>();
        var mobile = NewMobile();
        var staff = new CatEcommerce.Domain.Aggregates.Account.StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new CatEcommerce.Domain.Models.StaffAccountCreationDto(
            "Reset Me", CatEcommerce.Domain.ValueObjects.MobileNumber.Create(mobile), null, ["Nurse"],
            new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);

        var requestResponse = await client.PostAsJsonAsync("/api/staff/auth/password-reset/request", new { identifier = mobile }, Json);
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(staff.Id);
        Assert.NotNull(dispatched);

        const string newPassword = "newstaffpassword1";
        var confirm = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched!.PlainToken, newPassword }, Json);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password = newPassword }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // I-D5
    [Fact]
    public async Task SuccessfulReset_InvalidatesSessionsCreatedBeforeIt()
    {
        using var client = CreateClient();
        var (customerId, mobile, oldPassword) = await RegisterCustomerAsync(client);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = oldPassword }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // The client now carries a real session cookie, so further unsafe POSTs need the CSRF
        // header even though these particular endpoints are anonymous (ADR-032 applies per-caller,
        // not per-endpoint).
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);
        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(customerId)!;
        await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched.PlainToken, newPassword = "brandnewpassword1" }, Json);

        // The OLD session cookie (from before the reset) must now be invalid.
        var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }
}
