using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Walk-in (H).</summary>
public sealed class WalkInEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task<HttpClient> ReceptionClientAsync()
    {
        var (mobile, password) = await SeedReceptionAsync();
        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    private async Task<(string Mobile, string Password)> SeedReceptionAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Account.StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<CatEcommerce.Domain.DomainServices.IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new CatEcommerce.Domain.Aggregates.Account.StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new CatEcommerce.Domain.Models.StaffAccountCreationDto(
            "Front Desk", CatEcommerce.Domain.ValueObjects.MobileNumber.Create(mobile), null, ["Reception"],
            new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new CatEcommerce.Domain.Models.PasswordSetDto(hasher.Hash(password), new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);

        return (mobile, password);
    }

    // I-H1
    [Fact]
    public async Task Reception_CreatesWalkIn_201AndIncompleteReceptionChannel()
    {
        using var client = await ReceptionClientAsync();
        var walkInMobile = NewMobile();

        var response = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Walk-in Customer", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var customerId = created.GetProperty("customerId").GetGuid();

        var get = await client.GetAsync($"/api/staff/customers/{customerId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal("Incomplete", body.GetProperty("recordStatus").GetString());
        Assert.Equal("Reception", body.GetProperty("registrationChannel").GetString());
    }

    // HO-ACC-002 CR MEDIUM 2 (first-pass gate fix round, item 13): Q-H1 (GetStaffCustomer, the
    // fresh read MergeReview uses -- ADR-FE-011) used to report only catCount; MergeReview needs the
    // actual cats by name to show which are about to move in a merge, not just how many.
    [Fact]
    public async Task GetStaffCustomer_ReportsEveryOwnedCatByNameAlongsideCatCount()
    {
        using var client = await ReceptionClientAsync();
        var walkInMobile = NewMobile();

        var create = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Cat Owner", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var customerId = created.GetProperty("customerId").GetGuid();

        var registerCat = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = customerId, name = "Whiskers", sex = "Female", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerCat.StatusCode);

        var get = await client.GetAsync($"/api/staff/customers/{customerId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(1, body.GetProperty("catCount").GetInt32());
        var cats = body.GetProperty("cats").EnumerateArray().ToList();
        Assert.Single(cats);
        Assert.Equal("Whiskers", cats[0].GetProperty("name").GetString());
        Assert.False(cats[0].GetProperty("isDeceased").GetBoolean());
        Assert.NotEqual(Guid.Empty, cats[0].GetProperty("catId").GetGuid());
    }

    // I-H2
    [Fact]
    public async Task DuplicateMobile_Returns400AndFindEndpointLocatesExistingRecord()
    {
        using var client = await ReceptionClientAsync();
        var walkInMobile = NewMobile();

        var first = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Original Walk-in", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Duplicate Walk-in", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("MobileNumberAlreadyRegistered", await duplicate.Content.ReadAsStringAsync());

        var find = await client.GetAsync($"/api/staff/customers?mobile={Uri.EscapeDataString(walkInMobile)}");
        Assert.Equal(HttpStatusCode.OK, find.StatusCode);
        var results = await find.Content.ReadFromJsonAsync<JsonElement>(Json);
        var normalisedMobile = "+880" + walkInMobile[1..];
        Assert.Contains(results.EnumerateArray(), r => r.GetProperty("mobileNumber").GetString() == normalisedMobile);
    }

    // I-H3
    [Fact]
    public async Task WalkInCompletesViaReset_ThenSignsIn_RecordStatusComplete()
    {
        using var client = await ReceptionClientAsync();
        var walkInMobile = NewMobile();

        var create = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Completes Later", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var customerId = created.GetProperty("customerId").GetGuid();

        // The walk-in completes their own record through the ordinary customer-facing reset flow.
        using var customerClient = CreateClient();
        var requestReset = await customerClient.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.OK, requestReset.StatusCode);

        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(customerId);
        Assert.NotNull(dispatched);

        const string password = "completedpassword1";
        var confirm = await customerClient.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched!.PlainToken, newPassword = password }, Json);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var get = await client.GetAsync($"/api/staff/customers/{customerId}");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Complete", body.GetProperty("recordStatus").GetString());

        var signIn = await customerClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = walkInMobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // I-H4
    [Fact]
    public async Task WalkInTriesSelfRegistrationWithSameMobile_Returns400MobileAlreadyRegistered()
    {
        using var client = await ReceptionClientAsync();
        var walkInMobile = NewMobile();

        var create = await client.PostAsJsonAsync("/api/staff/customers", new { fullName = "Blocks Self-Registration", mobileNumber = walkInMobile }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // The "reset offer" itself is a client-side UX affordance shown for this error code, not
        // a distinct backend response shape -- the backend returns the same code as any other
        // duplicate-mobile registration (I-B4), confirming an Incomplete walk-in record still
        // blocks self-registration.
        using var guestClient = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(guestClient);
        var response = await guestClient.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Trying To Self Register", mobileNumber = walkInMobile, password = "correcthorsebattery",
            captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MobileNumberAlreadyRegistered", await response.Content.ReadAsStringAsync());
    }

    // SECURITY gate MEDIUM (walk-in hijack via additional numbers, HO-ACC-001 §1 Amended):
    // squatting on a victim's real mobile as someone else's UNVERIFIED additional number must
    // never block reception from registering the victim under their own, genuine primary number.
    [Fact]
    public async Task VictimsNumberSquattedAsSomeoneElsesAdditionalNumber_ReceptionCanStillRegisterTheVictim()
    {
        var victimMobile = NewMobile();

        // The "attacker" self-registers normally, then claims the victim's real number as one of
        // their own additional (never verified, never unique-checked) contact numbers.
        using var attackerClient = CreateClient();
        var (_, attackerMobile, attackerPassword) = await RegisterCustomerAsync(attackerClient);
        var attackerSignIn = await attackerClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = attackerMobile, password = attackerPassword }, Json);
        Assert.Equal(HttpStatusCode.OK, attackerSignIn.StatusCode);
        attackerClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(attackerSignIn, "pc_csrf"));

        var claim = await attackerClient.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Attacker", additionalPhoneNumbers = new[] { victimMobile }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);

        // Before the fix: this returned 400 MobileNumberAlreadyRegistered, because the additional-
        // number match hard-refused registration -- permanently blocking the real victim.
        using var receptionClient = await ReceptionClientAsync();
        var registerVictim = await receptionClient.PostAsJsonAsync("/api/staff/customers", new
        {
            fullName = "The Real Victim", mobileNumber = victimMobile
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerVictim.StatusCode);

        // Q-H2 must show both records for this number, distinguishing the genuine primary match
        // from the unverified additional-number claim.
        var find = await receptionClient.GetAsync($"/api/staff/customers?mobile={Uri.EscapeDataString(victimMobile)}");
        Assert.Equal(HttpStatusCode.OK, find.StatusCode);
        var results = await find.Content.ReadFromJsonAsync<JsonElement>(Json);
        var matches = results.EnumerateArray().ToList();

        Assert.Contains(matches, r => r.GetProperty("fullName").GetString() == "The Real Victim" &&
                                       r.GetProperty("mobileMatchedOn").GetString() == "Primary");
        Assert.Contains(matches, r => r.GetProperty("fullName").GetString() == "Attacker" &&
                                       r.GetProperty("mobileMatchedOn").GetString() == "Additional");
    }
}
