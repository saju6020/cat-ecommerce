using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Registration (B).</summary>
public sealed class RegistrationEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    // I-B1
    [Fact]
    public async Task Register_WithNameMobileAndPassword_Returns201()
    {
        using var client = CreateClient();
        var (customerId, _, _) = await RegisterCustomerAsync(client);
        Assert.NotEqual(Guid.Empty, customerId);
    }

    // I-B2
    [Fact]
    public async Task Register_WithEmailOnly_Returns201AndCanSignInByEmail()
    {
        using var client = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(client);
        var email = NewEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Email Only Customer", emailAddress = email, password = "correcthorsebattery",
            captchaId, captchaAnswer = answer
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var signInResponse = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = email, password = "correcthorsebattery" }, Json);
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
    }

    // I-B3
    [Fact]
    public async Task Register_WithNeitherIdentifier_Returns400WithIdentifierRequired()
    {
        using var client = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(client);

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "No Identifier", password = "correcthorsebattery", captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IdentifierRequired", body);
    }

    // I-B4
    [Fact]
    public async Task Register_SameMobileDifferentFormat_Returns400WithNoMentionOfEmail()
    {
        using var client = CreateClient();
        var (_, mobile, _) = await RegisterCustomerAsync(client);
        var differentFormat = "+880" + mobile[1..];

        var (captchaId, answer) = await GetCaptchaAsync(client);
        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Duplicate", mobileNumber = differentFormat, password = "correcthorsebattery",
            captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("MobileNumberAlreadyRegistered", body);
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
    }

    // I-B6
    [Fact]
    public async Task Register_ShortThenBreachedPassword_ReturnsDistinctCodes()
    {
        using var client = CreateClient();
        var mobile = NewMobile();

        var (shortCaptchaId, shortAnswer) = await GetCaptchaAsync(client);
        var shortResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Short Pw", mobileNumber = mobile, password = "short1",
            captchaId = shortCaptchaId, captchaAnswer = shortAnswer
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, shortResponse.StatusCode);
        Assert.Contains("PasswordTooShort", await shortResponse.Content.ReadAsStringAsync());

        var (breachedCaptchaId, breachedAnswer) = await GetCaptchaAsync(client);
        var breachedResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Breached Pw", mobileNumber = mobile, password = "123456789",
            captchaId = breachedCaptchaId, captchaAnswer = breachedAnswer
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, breachedResponse.StatusCode);
        Assert.Contains("PasswordTooShort", await breachedResponse.Content.ReadAsStringAsync()); // "123456789" is 9 chars -- still short

        var (breached2CaptchaId, breached2Answer) = await GetCaptchaAsync(client);
        var breached2Response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Breached Pw", mobileNumber = mobile, password = "1234567890",
            captchaId = breached2CaptchaId, captchaAnswer = breached2Answer
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, breached2Response.StatusCode);
        Assert.Contains("BreachedPassword", await breached2Response.Content.ReadAsStringAsync());
    }

    // I-B5
    [Fact]
    public async Task Register_SameEmailDifferentCase_Returns400WithNoMentionOfMobileOrPhone()
    {
        using var client = CreateClient();
        var email = NewEmail();

        var (firstCaptchaId, firstAnswer) = await GetCaptchaAsync(client);
        var first = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Original", emailAddress = email, password = "correcthorsebattery",
            captchaId = firstCaptchaId, captchaAnswer = firstAnswer
        }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var (captchaId, answer) = await GetCaptchaAsync(client);
        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Duplicate", emailAddress = email.ToUpperInvariant(), password = "correcthorsebattery",
            captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("EmailAddressAlreadyRegistered", body);
        Assert.DoesNotContain("mobile", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", body, StringComparison.OrdinalIgnoreCase);
    }

    // I-B10
    [Fact]
    public async Task Register_TwoConcurrentSameMobile_OneCreatedOneRejectedNeverBothOrServerError()
    {
        var mobile = NewMobile();

        async Task<HttpStatusCode> RegisterAsync()
        {
            using var client = CreateClient();
            var (captchaId, answer) = await GetCaptchaAsync(client);
            var response = await client.PostAsJsonAsync("/api/customers", new
            {
                fullName = "Concurrent", mobileNumber = mobile, password = "correcthorsebattery",
                captchaId, captchaAnswer = answer
            }, Json);
            return response.StatusCode;
        }

        var results = await Task.WhenAll(RegisterAsync(), RegisterAsync());

        Assert.Contains(HttpStatusCode.Created, results);
        Assert.Single(results, HttpStatusCode.Created);
        Assert.All(results, r => Assert.True(r is HttpStatusCode.Created or HttpStatusCode.BadRequest));

        var normalisedMobile = "+880" + mobile[1..]; // MobileNumber.Create's normal form (01XXXXXXXXX -> +8801XXXXXXXXX)
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var count = await db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == normalisedMobile);
        Assert.Equal(1, count);
    }

    // I-B7
    [Fact]
    public async Task Register_NoCaptchaFields_Returns400CaptchaRequired()
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "No Captcha", mobileNumber = NewMobile(), password = "correcthorsebattery"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CaptchaRequired", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WrongCaptchaAnswer_Returns400CaptchaInvalid()
    {
        using var client = CreateClient();
        var (captchaId, _) = await GetCaptchaAsync(client);

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Wrong Captcha", mobileNumber = NewMobile(), password = "correcthorsebattery",
            captchaId, captchaAnswer = "WRONG"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CaptchaInvalid", await response.Content.ReadAsStringAsync());
    }

    // I-B8
    [Fact]
    public async Task Register_ReplaySolvedChallenge_Returns400CaptchaInvalid()
    {
        using var client = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(client);

        var first = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "First", mobileNumber = NewMobile(), password = "correcthorsebattery", captchaId, captchaAnswer = answer
        }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Second", mobileNumber = NewMobile(), password = "correcthorsebattery", captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Contains("CaptchaInvalid", await replay.Content.ReadAsStringAsync());
    }

    // I-B11
    [Fact]
    public async Task Register_PersistedPasswordHash_IsArgon2idAndNeverContainsPlaintext()
    {
        using var client = CreateClient();
        var (customerId, _, password) = await RegisterCustomerAsync(client);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var customer = await db.CustomerAccounts.AsNoTracking().SingleAsync(c => c.Id == customerId);

        Assert.NotNull(customer.PasswordCredential);
        Assert.StartsWith("$argon2id$", customer.PasswordCredential!.Hash);
        Assert.DoesNotContain(password, customer.PasswordCredential.Hash);
    }

    // I-B12
    [Fact]
    public async Task GetCaptcha_ReturnsPngDataUrlAndNoStore()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/captcha", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.StartsWith("data:image/png;base64,", body.GetProperty("imageDataUrl").GetString());
        Assert.DoesNotContain("TEST1", await response.Content.ReadAsStringAsync());
    }
}
