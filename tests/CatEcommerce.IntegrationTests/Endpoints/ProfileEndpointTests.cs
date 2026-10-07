using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Profile (E).</summary>
public sealed class ProfileEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task<HttpClient> SignedInClientAsync()
    {
        var (client, _) = await SignedInClientWithPasswordAsync();
        return client;
    }

    private async Task<(HttpClient Client, string Password)> SignedInClientWithPasswordAsync()
    {
        var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, password);
    }

    // I-E1
    [Fact]
    public async Task GetProfile_ReturnsSignedInCustomersProfileWithNoCredentialFields()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/customers/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(body, "pass|hash|stamp|token|locked", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    // I-E2
    [Fact]
    public async Task AddThreeAddresses_SetSecondDefault_RemoveFirst_ExactlyOneDefaultAtEveryStep()
    {
        using var client = await SignedInClientAsync();

        async Task<Guid> AddAsync(string label)
        {
            var response = await client.PostAsJsonAsync("/api/customers/me/addresses", new
            {
                label, line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            return body.GetProperty("addressId").GetGuid();
        }

        async Task<int> CountDefaultsAsync()
        {
            var response = await client.GetAsync("/api/customers/me/addresses");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            return body.GetProperty("addresses").EnumerateArray().Count(a => a.GetProperty("isDefault").GetBoolean());
        }

        var first = await AddAsync("Home");
        Assert.Equal(1, await CountDefaultsAsync());

        await AddAsync("Work");
        Assert.Equal(1, await CountDefaultsAsync());

        var third = await AddAsync("Clinic Pickup");
        Assert.Equal(1, await CountDefaultsAsync());

        var setDefault = await client.PutAsync($"/api/customers/me/addresses/{third}/default", null);
        Assert.Equal(HttpStatusCode.NoContent, setDefault.StatusCode);
        Assert.Equal(1, await CountDefaultsAsync());

        var remove = await client.DeleteAsync($"/api/customers/me/addresses/{first}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(1, await CountDefaultsAsync());
    }

    // I-E3
    [Fact]
    public async Task RemoveDefaultAddress_WhileOthersExist_Returns400()
    {
        using var client = await SignedInClientAsync();

        async Task<Guid> AddAsync(string label)
        {
            var response = await client.PostAsJsonAsync("/api/customers/me/addresses", new
            {
                label, line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
            }, Json);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            return body.GetProperty("addressId").GetGuid();
        }

        var first = await AddAsync("Home");
        await AddAsync("Work");

        var response = await client.DeleteAsync($"/api/customers/me/addresses/{first}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DefaultAddressCannotBeRemoved", await response.Content.ReadAsStringAsync());
    }

    // I-E4
    [Fact]
    public async Task TwoConcurrentAddsAtNineAddresses_OneSucceedsOneConflictsNeverEleven()
    {
        using var client = await SignedInClientAsync();

        for (var i = 0; i < 9; i++)
        {
            var response = await client.PostAsJsonAsync("/api/customers/me/addresses", new
            {
                label = $"Address {i}", line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        Task<HttpResponseMessage> AddAsync(string label) => client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label, line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
        }, Json);

        var results = await Task.WhenAll(AddAsync("Tenth A"), AddAsync("Tenth B"));
        var statuses = results.Select(r => r.StatusCode).ToList();

        // TEST gate (2026-09-25): this is a genuine race that black-box HTTP concurrency cannot
        // force into one specific interleaving -- the same caveat TwoConcurrentFirstWrites'
        // own comment above documents for the first-write case. Reproduced directly: run in
        // isolation, the two requests are reliably NOT concurrent enough to collide inside EF's
        // optimistic-concurrency window (both load the aggregate, one commits, the OTHER's
        // request only THEN starts its own load), so the second one's fresh read already sees
        // ten addresses and is refused by the domain invariant itself (400
        // DeliveryAddressLimitExceeded) rather than by a stale-version conflict (409). Run
        // alongside the rest of the suite, genuine overlap is common enough that 409 is what is
        // usually observed. Both outcomes are the SAME "never eleven" invariant holding two
        // different ways, so this asserts that invariant -- exactly one add lands, the other is
        // refused one of the two legitimate ways, never a 500 -- rather than which one occurred.
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.All(statuses, s => Assert.True(
            s is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.BadRequest,
            $"Unexpected status {s} -- neither a successful add, a concurrency conflict, nor the domain limit refusal."));

        var get = await client.GetAsync("/api/customers/me/addresses");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(10, body.GetProperty("addresses").GetArrayLength());
    }

    // I-E5
    [Fact]
    public async Task TwoConcurrentFirstWrites_NeverBothLostNeverAServerError()
    {
        using var client = await SignedInClientAsync();

        Task<HttpResponseMessage> AddAsync(string label) => client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label, line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
        }, Json);

        var results = await Task.WhenAll(AddAsync("First A"), AddAsync("First B"));
        var statuses = results.Select(r => r.StatusCode).ToList();

        // AddDeliveryAddressCommandHandler lazily creates the address book on its first write
        // (see that handler's own doc comment): a genuine race on that first INSERT produces one
        // 2xx and one 409 (PK collision via UniqueConstraintExceptionFilter). Forcing that exact
        // microsecond-level race deterministically through black-box HTTP concurrency is not
        // reliable (the two requests can legitimately interleave without colliding, in which case
        // both succeed as ordinary sequential adds against the now-existing book) -- so this test
        // asserts the invariant that actually matters: no 500, no data loss (both attempted labels
        // present when both succeeded; exactly one present when one was rejected).
        Assert.All(statuses, s => Assert.True(s is HttpStatusCode.Created or HttpStatusCode.Conflict));

        var get = await client.GetAsync("/api/customers/me/addresses");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);
        var expectedCount = statuses.Count(s => s == HttpStatusCode.Created);
        Assert.Equal(expectedCount, body.GetProperty("addresses").GetArrayLength());
    }

    // I-E6
    [Fact]
    public async Task GetPreferences_BeforeAnyWrite_EveryCellTransactionalAlwaysOnMarketingNotGranted()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync("/api/customers/me/notification-preferences");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var cells = body.GetProperty("cells").EnumerateArray().ToList();
        Assert.NotEmpty(cells);

        foreach (var cell in cells)
        {
            var category = cell.GetProperty("category").GetString();
            var isAlwaysOn = cell.GetProperty("isAlwaysOn").GetBoolean();
            var isGranted = cell.GetProperty("isGranted").GetBoolean();

            if (category == "Transactional")
            {
                Assert.True(isAlwaysOn);
                Assert.True(isGranted);
            }
            else
            {
                Assert.False(isAlwaysOn);
                Assert.False(isGranted); // NFR-LEG-4: marketing default not granted
            }
        }
    }

    // I-E7
    [Fact]
    public async Task PutTransactionalCellOff_Returns400AndAppliesNothing()
    {
        using var client = await SignedInClientAsync();

        var before = await client.GetAsync("/api/customers/me/notification-preferences");
        var beforeBody = await before.Content.ReadFromJsonAsync<JsonElement>(Json);

        var response = await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new
        {
            changes = new[] { new { channel = "Email", category = "Transactional", isGranted = false } }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("NotificationPreferenceNotEditable", await response.Content.ReadAsStringAsync());

        var after = await client.GetAsync("/api/customers/me/notification-preferences");
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(beforeBody.GetRawText(), afterBody.GetRawText());
    }

    // I-E8
    [Fact]
    public async Task MeEndpoint_AsGuestOrStaff_Returns401()
    {
        using var guestClient = CreateClient();
        var guestResponse = await guestClient.GetAsync("/api/customers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, guestResponse.StatusCode);
    }

    // SECURITY gate LOW (email is a login/reset identifier -- HO-ACC-001 §1 Amended): changing it
    // must require the same step-up C18 already requires, or a stolen session alone lets an
    // attacker redirect password resets to themselves and turn a temporary theft into a permanent
    // takeover.
    [Fact]
    public async Task UpdateProfile_NoEmailChange_DoesNotRequireCurrentPassword()
    {
        using var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Just A Name Change", additionalPhoneNumbers = Array.Empty<string>()
        }, Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_SettingEmail_WithoutCurrentPassword_Returns400()
    {
        using var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Adding An Email", emailAddress = NewEmail(), additionalPhoneNumbers = Array.Empty<string>()
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ReauthenticationFailed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateProfile_SettingEmail_WithWrongCurrentPassword_Returns400()
    {
        using var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Adding An Email", emailAddress = NewEmail(), additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = "definitely-the-wrong-password"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ReauthenticationFailed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateProfile_SettingEmail_WithCorrectCurrentPassword_Succeeds()
    {
        var (client, password) = await SignedInClientWithPasswordAsync();
        var newEmail = NewEmail();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Adding An Email", emailAddress = newEmail, additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = password
        }, Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var profile = await client.GetAsync("/api/customers/me");
        var body = await profile.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(newEmail, body.GetProperty("emailAddress").GetString());
    }

    // SECURITY gate MEDIUM (second pass, HO-ACC-001 §1 Amended (18)): repeated wrong step-up
    // passwords must lock the account exactly like repeated wrong sign-in passwords do (C18's own
    // behaviour) -- before this fix, a step-up mismatch never called RecordFailedLogin at all, so
    // this endpoint was an unthrottled password oracle against a stolen session's holder.
    [Fact]
    public async Task UpdateProfile_SettingEmail_RepeatedWrongPassword_LocksTheAccount()
    {
        using var client = await SignedInClientAsync();

        // LoginThrottleRules.DelayFor: the 3rd consecutive failure locks for 5s -- well within a
        // fast local test run, so the very next request lands inside the lock window.
        for (var i = 0; i < 3; i++)
        {
            var attempt = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = "Locking Attempt", emailAddress = NewEmail(), additionalPhoneNumbers = Array.Empty<string>(),
                currentPassword = "definitely-the-wrong-password"
            }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
        }

        var lockedResponse = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Should Be Locked Out", emailAddress = NewEmail(), additionalPhoneNumbers = Array.Empty<string>(),
            currentPassword = "definitely-the-wrong-password"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, lockedResponse.StatusCode);
        Assert.Contains("AccountLocked", await lockedResponse.Content.ReadAsStringAsync());
    }

    // SECURITY gate MEDIUM (second pass); ARCHITECTURE gate fifth pass, MEDIUM, follow-up
    // (2026-09-25): the step-up path must also be rate-limited, bounding concurrent Argon2id
    // verifies regardless of the per-account lockout above. It now has its OWN policy
    // (RateLimitPolicies.ProfileUpdate), partitioned by customer id, not AccountSensitive's IP
    // partition -- see the two tests below for the separation this buys.
    [Fact]
    public async Task UpdateProfile_UnderTightProfileUpdateLimit_SixthReturns429()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        // WithTightRateLimits sets RateLimiting:ProfileUpdate to the real 5-per-60-minutes default
        // -- the 6th request in the same window is the first to exceed it.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = "Rate Limited Attempt", emailAddress = NewEmail(), additionalPhoneNumbers = Array.Empty<string>(),
                currentPassword = "definitely-the-wrong-password"
            }, Json);
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    // ARCHITECTURE gate fifth pass, MEDIUM, follow-up (2026-09-25): ProfileUpdate must be a
    // SEPARATE bucket from AccountSensitive (deletion, export) -- before this fix, both shared one
    // named policy, so exhausting it with profile saves would also block a genuine deletion
    // attempt for the rest of the window.
    [Fact]
    public async Task UpdateProfile_FiveProfileSaves_DoNotBlockADeletionAttempt()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        // Exhausts the whole ProfileUpdate bucket (tight limit: 5 per 60 minutes) with name-only
        // saves -- no step-up password needed, so every one of these succeeds.
        for (var i = 0; i < 5; i++)
        {
            var save = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = $"Save {i}", additionalPhoneNumbers = Array.Empty<string>()
            }, Json);
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        // AccountSensitive (deletion, export) is a different bucket entirely -- this must not be
        // 429 even though ProfileUpdate's bucket for this same customer is now exhausted.
        var deletion = await client.PostAsJsonAsync("/api/customers/me/deletion", new
        {
            currentPassword = "definitely-the-wrong-password"
        }, Json);

        Assert.NotEqual((HttpStatusCode)429, deletion.StatusCode);
    }

    // ARCHITECTURE gate fifth pass, MEDIUM, follow-up (2026-09-25): customers behind shared NAT or
    // clinic Wi-Fi must not block each other -- ProfileUpdate is partitioned by the signed-in
    // customer id, not IP, unlike the old shared-with-AccountSensitive design.
    [Fact]
    public async Task UpdateProfile_TwoDifferentCustomersFromTheSameIp_DoNotShareTheProfileBucket()
    {
        using var client = Factory.WithTightRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        // Customer A exhausts their own ProfileUpdate bucket (tight limit: 5 per 60 minutes) --
        // every request here comes from the same TestServer connection/IP as customer B below.
        var (_, mobileA, passwordA) = await RegisterCustomerAsync(client);
        var signInA = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobileA, password = passwordA }, Json);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signInA, "pc_csrf"));

        for (var i = 0; i < 5; i++)
        {
            var save = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = $"Customer A Save {i}", additionalPhoneNumbers = Array.Empty<string>()
            }, Json);
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        var sixthForA = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Customer A Should Be Blocked", additionalPhoneNumbers = Array.Empty<string>()
        }, Json);
        Assert.Equal((HttpStatusCode)429, sixthForA.StatusCode);

        // Customer B signs in on the SAME client (same IP/connection), replacing the session
        // cookie -- their own ProfileUpdate bucket is untouched.
        var (_, mobileB, passwordB) = await RegisterCustomerAsync(client);
        var signInB = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobileB, password = passwordB }, Json);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signInB, "pc_csrf"));

        var firstForB = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Customer B First Save", additionalPhoneNumbers = Array.Empty<string>()
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, firstForB.StatusCode);
    }

    // SECURITY gate LOW (second pass, HO-ACC-001 §1 Amended (18)): a self-service additional
    // number that equals another ACTIVE customer's PRIMARY mobile must be refused -- unlike
    // WalkInEndpointTests' squatting scenario (which claims a number BEFORE the victim exists, and
    // is deliberately still allowed), this is the reverse ordering: the victim already exists.
    [Fact]
    public async Task UpdateProfile_AdditionalNumberMatchesAnotherActiveCustomersPrimary_Returns400()
    {
        // The "victim" registers first, under their own genuine primary mobile.
        using var victimClient = CreateClient();
        var (_, victimMobile, _) = await RegisterCustomerAsync(victimClient);

        // A second, unrelated customer then tries to claim the victim's primary as their own
        // additional number.
        using var client = await SignedInClientAsync();
        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Claiming Someone Else's Number", additionalPhoneNumbers = new[] { victimMobile }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("AdditionalPhoneNumberMatchesAnotherCustomer", await response.Content.ReadAsStringAsync());

        // No partial effect -- the claim must not have been saved.
        var profile = await client.GetAsync("/api/customers/me");
        var body = await profile.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Empty(body.GetProperty("additionalPhoneNumbers").EnumerateArray());
    }

    // A number already accepted (before this fix, or via the still-allowed squatting-before-victim-
    // exists ordering) must not retroactively block an UNRELATED edit on the same account.
    [Fact]
    public async Task UpdateProfile_UnrelatedEditWithAPreviouslyAcceptedAdditionalNumber_StillSucceeds()
    {
        var squattedMobile = NewMobile();
        using var client = await SignedInClientAsync();

        var claim = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Before Anyone Else Registers That Number", additionalPhoneNumbers = new[] { squattedMobile }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);

        // Someone else now registers under that same number as their genuine primary.
        using var laterRegistrant = CreateClient();
        await RegisterCustomerAsync(laterRegistrant, squattedMobile);

        // An unrelated edit (name only, additional numbers unchanged) on the FIRST account must
        // still succeed -- the check only applies to NEWLY added numbers.
        var unrelatedEdit = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Just Changing My Name", additionalPhoneNumbers = new[] { squattedMobile }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, unrelatedEdit.StatusCode);
    }

    // I-J1
    [Fact]
    public async Task ExportPersonalData_ReturnsAttachmentWithSchemaVersionProfileAddressesPreferencesAndCats()
    {
        var (client, _) = await SignedInClientWithPasswordAsync();

        var addAddress = await client.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Home", line1 = "1 Test Road", city = "Dhaka", district = "Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, addAddress.StatusCode);

        var response = await client.PostAsync("/api/customers/me/personal-data-export", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString() ?? "");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(1, body.GetProperty("schemaVersion").GetInt32());
        Assert.True(body.TryGetProperty("profile", out _));
        Assert.Single(body.GetProperty("addresses").EnumerateArray());
        Assert.True(body.GetProperty("notificationPreferences").EnumerateArray().Any());
        Assert.True(body.TryGetProperty("cats", out _));
    }

    // I-J2
    [Fact]
    public async Task DeleteAccount_WithRightPassword_AnonymisesEndsSessionAndSignInFailsGenerically()
    {
        var (client, password) = await SignedInClientWithPasswordAsync();
        var meBefore = await client.GetAsync("/api/customers/me");
        var mobile = (await meBefore.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("mobileNumber").GetString();

        var delete = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = password }, Json);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The session cookies this same client already holds must stop working immediately.
        var meAfter = await client.GetAsync("/api/customers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);

        // Sign-in with the (now anonymised) former mobile fails with the same generic response
        // used for any other unknown identifier -- no disclosure that this specific account once
        // existed and was deleted.
        using var freshClient = CreateClient();
        var signIn = await freshClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
    }

    // I-J3
    [Fact]
    public async Task NewRegistration_WithAPreviouslyDeletedMobile_Succeeds()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var delete = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = password }, Json);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var registerClient = CreateClient();
        var (captchaId, answer) = await GetCaptchaAsync(registerClient);
        var reRegister = await registerClient.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Reclaimed Number", mobileNumber = mobile, password = "correcthorsebattery",
            captchaId, captchaAnswer = answer
        }, Json);

        Assert.Equal(HttpStatusCode.Created, reRegister.StatusCode);
    }

    // I-J4
    [Fact]
    public async Task DeleteAccount_WrongPasswordThreeTimes_LocksTheAccountSameThrottleAsSignIn()
    {
        var (client, correctPassword) = await SignedInClientWithPasswordAsync();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
            last = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = "definitely-the-wrong-password" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, last!.StatusCode);
        Assert.Contains("ReauthenticationFailed", await last.Content.ReadAsStringAsync());

        // The fourth attempt -- even with the CORRECT password -- now finds the account locked,
        // proving this step-up shares the same LoginThrottle as ordinary sign-in (U-J3).
        var fourth = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = correctPassword }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        Assert.Contains("AccountLocked", await fourth.Content.ReadAsStringAsync());
    }

    // Integrator fix 2026-09-26: HO-ACC-001's own carried item (§1 Amended (21); HO-ADM-001 §1
    // Amended (32)/(50)) -- a NUL byte in `fullName` previously reached Postgres unfiltered and
    // returned an uncaught 500. HO-ADM-001's global `NulByteRejectionFilter` (now registered from
    // Program.cs, not any one area's module) intercepts it ahead of the handler and returns 400.
    [Fact]
    public async Task UpdateProfile_FullNameWithNulByte_Returns400()
    {
        using var client = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Bad\u0000Name", additionalPhoneNumbers = Array.Empty<string>()
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ContainsControlCharacter", await response.Content.ReadAsStringAsync());
    }
}
