using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// SECURITY gate MEDIUM: a locked, not-active or no-password account must cost the same one
/// Argon2 verify as a real wrong-password attempt, or its response time (and, provably here, its
/// call to the hasher) discloses account state -- exactly what R1/ADR-028 exist to prevent. Uses a
/// <see cref="SpyPasswordHasher"/> rather than wall-clock timing, per the gate's own guidance.
/// </summary>
public sealed class TimingEnumerationTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public async Task CustomerSignIn_LockedAccount_StillCallsVerifyAgainstDummy()
    {
        var spyFactory = Factory.WithHasherSpy();
        using var client = spyFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var (_, mobile, _) = await RegisterCustomerAsync(client);

        // LoginThrottleRules.DelayFor: the 3rd consecutive failure locks the account for 5s.
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "wrongwrongwrong" }, Json);

        SpyPasswordHasher.Reset();

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "wrongwrongwrong" }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(SpyPasswordHasher.VerifyAgainstDummyCalls >= 1,
            "The locked-account refusal branch must call VerifyAgainstDummy, exactly like the unknown-identifier path.");
    }

    [Fact]
    public async Task StaffSignIn_LockedAccount_StillCallsVerifyAgainstDummy()
    {
        var spyFactory = Factory.WithHasherSpy();
        using var client = spyFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        await using var scope = spyFactory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Timing Test Staff", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        staff.SetPassword(new PasswordSetDto(hasher.Hash("staffpassword1"), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password = "wrongwrongwrong" }, Json);

        SpyPasswordHasher.Reset();

        var response = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password = "wrongwrongwrong" }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(SpyPasswordHasher.VerifyAgainstDummyCalls >= 1,
            "The locked-account refusal branch must call VerifyAgainstDummy, exactly like the unknown-identifier path.");
    }
}
