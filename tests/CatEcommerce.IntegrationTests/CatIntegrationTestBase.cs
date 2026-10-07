using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-CAT-001 §16. Shared helpers for real-HTTP tests against the Cat Profiles API.</summary>
public abstract class CatIntegrationTestBase(CatApiFactory factory) : CatEcommerceIntegrationTestBase<CatApiFactory>(factory)
{
    /// <summary>HO-ADM-001 §12.3 / ADR-043: <see cref="RegisterCatAsync"/> now signs in a staff
    /// session on the client, which needs its cookie explicitly handled -- matching
    /// <c>AccountsIntegrationTestBase</c>/<c>AdministrationIntegrationTestBase</c>'s own override.</summary>
    protected override HttpClient CreateClient() => Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    /// <summary>
    /// Registers a cat under a fresh, per-test owner id -- §16.1 rule 1: every test mints its own
    /// ids and its own owner, never a fixed literal.
    ///
    /// HO-ADM-001 §12.3 / ADR-043: <c>Cats.Register</c> now requires a signed-in staff or customer
    /// caller. This seeds a fresh Administrator and signs in ON <paramref name="client"/> itself
    /// (rather than a separate internal client) so every caller of this helper -- which typically
    /// keeps using the same <paramref name="client"/> for follow-up calls on the same cat --
    /// continues to work unchanged; Administrator holds every Cat action (§12.3's table), so no
    /// existing follow-up call becomes newly forbidden. A caller that needs a specific,
    /// already-authenticated identity should not use this helper.
    /// </summary>
    protected async Task<Guid> RegisterCatAsync(HttpClient client, Guid? ownerId = null, string name = "Integration Test Cat")
    {
        await SignInAsFreshAdministratorAsync(client);

        // SECURITY gate, LOW (2026-09-26): a staff-supplied ownerId must now resolve to a real,
        // non-deleted CustomerAccountAggregate (RegisterCatCommandHandler). When the caller does
        // not pin a specific owner id, seed a genuine customer rather than a bare Guid.NewGuid(),
        // which the handler would now reject with CustomerNotFound.
        var resolvedOwnerId = ownerId ?? await SeedCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = resolvedOwnerId,
            name,
            species = "Cat",
            sex = "Female",
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            dateOfBirthPrecision = "Exact"
        }, Json);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("catId").GetGuid();
    }

    /// <summary>
    /// SECURITY gate, MEDIUM (2026-09-26): AdministeredByVetId/AdministeredByStaffId now must
    /// resolve to an existing, active staff account (a Vet, for the vet field). Seeds one
    /// directly through the repository -- StaffAccountsController's own creation endpoint is
    /// Administrator-only, and standing up a signed-in admin session is HO-ACC-001's own test
    /// surface, not Cat's. A fresh mobile number per call keeps this §16.1-safe on a shared
    /// database (MobileNumber is unique per HO-ACC-001).
    /// </summary>
    protected async Task<Guid> SeedStaffAccountAsync(string role)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();

        var staff = new StaffAccountAggregate(Guid.NewGuid());
        var mobile = MobileNumber.Create($"017{Random.Shared.Next(10000000, 99999999)}");
        staff.Create(new StaffAccountCreationDto(
            "Integration Test Staff", mobile, null, [role],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
        return staff.Id;
    }

    /// <summary>
    /// SECURITY gate, LOW (2026-09-26): a staff-supplied OwnerId (register) or NewOwnerId
    /// (ownership-transfer) must now resolve to a real, non-deleted CustomerAccountAggregate.
    /// Seeds one directly through the repository (the "walk-in" shape -- no credential needed for
    /// a caller that only wants an owner id to exist), with a fresh mobile number per call, the
    /// same §16.1-safe pattern <see cref="SeedStaffAccountAsync"/> uses.
    /// </summary>
    protected async Task<Guid> SeedCustomerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();

        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        var mobile = MobileNumber.Create($"018{Random.Shared.Next(10000000, 99999999)}");
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto(
            "Integration Test Customer", mobile,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(customer, EventPublishOption.InMemoryPublishOnly);
        return customer.Id;
    }

    /// <summary>Exposed (not just used by <see cref="RegisterCatAsync"/>) for a test that needs an
    /// authenticated staff caller without also registering a cat, e.g. probing an unknown cat id.</summary>
    protected async Task SignInAsFreshAdministratorAsync(HttpClient client)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Test Administrator", MobileNumber.Create(mobile), null, ["Administrator"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        signIn.EnsureSuccessStatusCode();

        var setCookies = signIn.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var csrfHeader = setCookies.First(c => c.StartsWith("pc_csrf=", StringComparison.Ordinal));
        var csrfValue = Uri.UnescapeDataString(csrfHeader["pc_csrf=".Length..].Split(';')[0]);

        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrfValue);
    }
}
