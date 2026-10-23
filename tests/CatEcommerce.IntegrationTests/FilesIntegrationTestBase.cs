using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-FIL-001 §16.1 rules apply unchanged (fresh ids per test, never assert emptiness,
/// never TRUNCATE). Helpers mint fresh staff/customers per test and build multipart upload
/// requests, mirroring <c>AdministrationIntegrationTestBase</c>'s pattern.</summary>
public abstract class FilesIntegrationTestBase(FilesApiFactory factory)
    : CatEcommerceIntegrationTestBase<FilesApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        var rawValue = header[(cookieName.Length + 1)..].Split(';')[0];
        return Uri.UnescapeDataString(rawValue);
    }

    /// <summary>Seeds a staff account with the given roles directly through the repository (§16.1
    /// rule 7), gives it a password, then returns credentials for a real sign-in.</summary>
    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Test Staff", MobileNumber.Create(mobile), null, roles,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, mobile, password);
    }

    protected async Task<HttpClient> SignInStaffAsync(string mobile, string password)
    {
        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    /// <summary>Registers and signs in a fresh staff caller with the given roles in one call.</summary>
    protected async Task<HttpClient> SignedInStaffClientAsync(params string[] roles)
    {
        var (_, mobile, password) = await SeedStaffAsync(roles);
        return await SignInStaffAsync(mobile, password);
    }

    /// <summary>Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): the customer
    /// upload-then-attach-a-cat-photo path (§1 Amended (10)) needs a real customer and a real cat --
    /// mirrors <c>AdministrationIntegrationTestBase</c>'s identical helpers.</summary>
    protected async Task<(HttpClient Client, Guid CustomerId, string Mobile)> RegisterAndSignInCustomerAsync()
    {
        var client = CreateClient();
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaBody = await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var captchaId = captchaBody.GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";

        var register = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test Customer", mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registerBody = await register.Content.ReadFromJsonAsync<JsonElement>(Json);
        var customerId = registerBody.GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        return (client, customerId, mobile);
    }

    /// <summary>Seeds a cat directly through the repository, owned by <paramref name="ownerId"/>.</summary>
    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Test Cat")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, today.AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", today,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>Builds a <c>multipart/form-data</c> request with one file part, matching what a
    /// real browser upload sends -- used against <c>POST /api/staff/files</c> and
    /// <c>POST /api/customers/me/files</c>.</summary>
    protected static MultipartFormDataContent BuildUpload(byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    // ── Well-known minimal-but-valid file bytes for each magic-byte check ──────────────────────
    protected static byte[] MinimalPng() => TestImages.Png();

    protected static byte[] MinimalPdf() => "%PDF-1.4\n%%EOF"u8.ToArray();

    protected const string EicarString = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
}
