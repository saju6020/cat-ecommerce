using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 TEST gate (written first, before running anything -- see
/// docs/test-cases/TC-ADM-001.md). Two case groups the SECURITY/ARCHITECTURE fifth-pass gates named
/// explicitly as owed to this gate and found, on inspection, to have no covering test anywhere in
/// the tree:
///
/// 1. I-A3 (§16) says a disallowed call gets "one <c>AuthorizationDenied</c> audit row" -- every
///    existing 403 test (<c>CatVaccineAuthorizationSecurityTests</c>,
///    <c>AdminContentPolicyEndpointTests</c>'s Policies.Manage cases) asserts the status code and the
///    response body's violation code, but none of them then searches the audit log to confirm
///    <c>RecordAuthorizationDenialCommandHandler</c> actually wrote the row §12.3/ADR-043 promises.
///
/// 2. The ARCHITECTURE and SECURITY fifth-pass gates both flagged, as a question for TEST/SECURITY to
///    confirm (not merely reason about), the global <c>NulByteRejectionFilter</c>'s effect on areas
///    outside Administration: Accounts sign-in goes from a generic 401 to 400 on a NUL byte, and
///    customer registration now rejects a NUL before the CAPTCHA is ever checked or consumed. SECURITY
///    pass 5 ran ad-hoc probes for this (see docs/gates/HO-ADM-001-SECURITY.md) but no probe became a
///    committed, repeatable test in either area's suite.</summary>
public sealed class AdminAuthorizationDenialAndNulFilterCrossAreaEndpointTests(AdministrationApiFactory factory)
    : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode(string prefix = "T") => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ---------- I-A3: a disallowed call is refused AND leaves exactly one AuthorizationDenied row ----------

    [Fact]
    public async Task NurseMarksCatDeceased_Returns403_AndWritesOneAuthorizationDeniedAuditRow()
    {
        var ownerId = Guid.NewGuid();
        var catId = await SeedCatAsync(ownerId);

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var client = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);

        var response = await client.PostAsJsonAsync(
            $"/api/cats/{catId}/deceased",
            new { deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") },
            Json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var to = DateTimeOffset.UtcNow.AddMinutes(5);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var search = await adminClient.GetAsync(
            $"/api/admin/audit-entries?actorUserId={nurse.StaffAccountId}&entityType=Authorization&action=AuthorizationDenied" +
            $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(items);
        var row = items[0];
        Assert.Equal(nurse.StaffAccountId, row.GetProperty("actorUserId").GetGuid());
        Assert.Equal("AuthorizationDenied", row.GetProperty("action").GetString());
        Assert.Equal(Actions.Cats.MarkDeceased, row.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ShopManagerPublishesPolicyVersion_Returns403_AndWritesOneAuthorizationDeniedAuditRow()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var key = "policy-" + Guid.NewGuid().ToString("N")[..10];
        var createPolicy = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms", requiresAcceptance = true }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);

        var shopManager = await SeedStaffAsync(StaffRoles.ShopManager);
        using var shopManagerClient = await SignInStaffAsync(shopManager.Mobile, shopManager.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);

        var publish = await shopManagerClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);

        var to = DateTimeOffset.UtcNow.AddMinutes(5);

        var search = await adminClient.GetAsync(
            $"/api/admin/audit-entries?actorUserId={shopManager.StaffAccountId}&entityType=Authorization&action=AuthorizationDenied" +
            $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal(Actions.Policies.Manage, items[0].GetProperty("reason").GetString());
    }

    // ---------- Global NulByteRejectionFilter, cross-area effect on Accounts (ARCH pass-5 MEDIUM 1) ----------

    [Fact]
    public async Task CustomerSignIn_NulInIdentifier_Returns400ContainsControlCharacter_NotInvalidCredentials()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = "bad\0mobile", password = "whatever1" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.ContainsControlCharacter, text);
        Assert.DoesNotContain("invalid_credentials", text);
    }

    [Fact]
    public async Task CustomerSignIn_NulInPassword_Returns400ContainsControlCharacter_NotInvalidCredentials()
    {
        var (_, _, mobile) = await RegisterAndSignInCustomerAsync();
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "bad\0password" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.ContainsControlCharacter, text);
        Assert.DoesNotContain("invalid_credentials", text);
    }

    [Fact]
    public async Task StaffSignIn_NulInIdentifier_Returns400ContainsControlCharacter_NotInvalidCredentials()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = "bad\0mobile", password = "whatever1" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.ContainsControlCharacter, text);
        Assert.DoesNotContain("invalid_credentials", text);
    }

    [Fact]
    public async Task CustomerSignIn_CorrectCredentialsAfterANulAttempt_StillSucceeds()
    {
        var (_, _, mobile) = await RegisterAndSignInCustomerAsync();
        using var client = CreateClient();

        var nulAttempt = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "bad\0password" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, nulAttempt.StatusCode);

        var realAttempt = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "correcthorsebattery" }, Json);
        Assert.Equal(HttpStatusCode.OK, realAttempt.StatusCode);
    }

    // ---------- Global NulByteRejectionFilter, ordering ahead of CaptchaValidationFilter ----------

    [Fact]
    public async Task Registration_NulInFullName_NoCaptchaSupplied_Returns400ContainsControlCharacter_NotCaptchaRequired()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Bad\0Name", mobileNumber = NewMobile(), password = "correcthorsebattery"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.ContainsControlCharacter, text);
        Assert.DoesNotContain(BusinessViolationCodes.CaptchaRequired, text);
    }

    [Fact]
    public async Task Registration_NulInFullName_WrongCaptchaAnswer_Returns400ContainsControlCharacter_NotCaptchaInvalid()
    {
        using var client = CreateClient();
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaBody = await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var captchaId = captchaBody.GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Bad\0Name", mobileNumber = mobile, password = "correcthorsebattery",
            captchaId, captchaAnswer = "WRONG"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.ContainsControlCharacter, text);
        Assert.DoesNotContain(BusinessViolationCodes.CaptchaInvalid, text);

        // No account was created: the same mobile can still register cleanly afterwards.
        var captchaResponse2 = await client.PostAsync("/api/captcha", null);
        captchaResponse2.EnsureSuccessStatusCode();
        var captchaBody2 = await captchaResponse2.Content.ReadFromJsonAsync<JsonElement>(Json);
        var captchaId2 = captchaBody2.GetProperty("captchaId").GetString()!;

        var retry = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Good Name", mobileNumber = mobile, password = "correcthorsebattery",
            captchaId = captchaId2, captchaAnswer = "TEST1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }
}
