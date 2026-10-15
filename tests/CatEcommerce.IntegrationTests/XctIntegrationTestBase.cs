using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-XCT-001 section 16 -- the rules of section 16.1 (HO-ACC-001) and the three extra rules apply: purge and redaction cases
/// seed their own rows with ids they record and assert only on those ids (9); protected rows are never cleaned up (10);
/// a past row is created through the repository under a past clock, never by a raw back-dating UPDATE (11).
/// </summary>
public abstract class XctIntegrationTestBase(XctApiFactory factory) : CatEcommerceIntegrationTestBase<XctApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static HttpClient NewClient(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static UserContext AnyActor() => new(Guid.NewGuid(), Guid.NewGuid());

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    /// <summary>Opens a connection to this branch's own database (never dev/test -- the factory guard has already run).</summary>
    protected static async Task<NpgsqlConnection> OpenDatabaseAsync()
    {
        var connection = new NpgsqlConnection(XctApiFactory.GuardedConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    protected static async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(
        WebApplicationFactory<Program>? factory = null, params string[] roles)
    {
        await using var scope = (factory ?? Factory).Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto("Test Staff", MobileNumber.Create(mobile), null, roles, AnyActor(), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), AnyActor(), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, mobile, password);
    }

    protected Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles) => SeedStaffAsync(null, roles);

    protected async Task<HttpClient> SignInStaffAsync(string mobile, string password, WebApplicationFactory<Program>? factory = null)
    {
        var client = factory is null ? CreateClient() : NewClient(factory);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    protected async Task<(HttpClient Client, Guid StaffAccountId)> SignedInStaffAsync(string role, WebApplicationFactory<Program>? factory = null)
    {
        var (id, mobile, password) = await SeedStaffAsync(factory, role);
        return (await SignInStaffAsync(mobile, password, factory), id);
    }

    protected async Task<(HttpClient Client, Guid CustomerId, string Mobile, string Password)> RegisterAndSignInCustomerAsync(WebApplicationFactory<Program>? factory = null)
    {
        var client = factory is null ? CreateClient() : NewClient(factory);
        var captcha = await client.PostAsync("/api/captcha", null);
        captcha.EnsureSuccessStatusCode();
        var captchaId = (await captcha.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";
        var register = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test Customer", mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var customerId = (await register.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, customerId, mobile, password);
    }

    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Test Cat", WebApplicationFactory<Program>? factory = null)
    {
        await using var scope = (factory ?? Factory).Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(DateTime.UtcNow), AnyActor(), Guid.NewGuid()));
        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>Runs <paramref name="action"/> with a database context from a fresh scope (reads and test seeding).</summary>
    protected async Task<T> WithDbAsync<T>(Func<CatEcommerceDbContext, Task<T>> action, WebApplicationFactory<Program>? factory = null)
    {
        await using var scope = (factory ?? Factory).Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>());
    }

    protected async Task<int> CountAuditAsync(string action, Guid entityId) =>
        await WithDbAsync(db => db.Database.SqlQuery<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM audit.audit_entry WHERE \"Action\" = {action} AND \"EntityId\" = {entityId}").SingleAsync());
}
