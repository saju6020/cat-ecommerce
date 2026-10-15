using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-POR-001 §16.1 -- every test mints its own customers (registration with the CAPTCHA test answer) and signs in; the dashboard is
/// asserted only for that customer; upstream data is arranged through the owning modules' own endpoints (customer sessions for cats and
/// appointments, administrator sessions for branches, services and the deceased flag), never by inserting rows. No global count is asserted.
/// Helpers take an optional <c>host</c> so a test that derived a factory with <c>WithWebHostBuilder</c> (a throwing fake, captured logs) can
/// use the same arrangement code on it.
/// </summary>
public abstract class PortalIntegrationTestBase(PortalApiFactory factory)
    : CatEcommerceIntegrationTestBase<PortalApiFactory>(factory)
{
    protected const string StaffPassword = "staffpassword1";
    protected const string CustomerPassword = "correcthorsebattery";

    protected override HttpClient CreateClient() => ClientOn(Factory);

    protected static HttpClient ClientOn(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewCode() => "P" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    /// <summary>
    /// A bookable start that does not depend on the time of day the suite runs: 10:00 Asia/Dhaka (UTC+6, no DST) on the third branch-local day
    /// after the clock's Dhaka date, as a UTC instant. 10:00 is on the 15-minute grid and a 30-minute service starting then ends well inside the
    /// test clinic's 00:00-23:45 hours. (The earlier "now + 3 days, rounded down" choice landed on 23:30 Dhaka when the suite ran in the last
    /// half hour of the Dhaka day, so the 30-minute service ended after closing and the hold failed with 409 SlotNoLongerAvailable.)
    /// </summary>
    protected DateTimeOffset BookableSlot(int daysAhead = 3)
    {
        var dhakaOffset = TimeSpan.FromHours(6);
        var dhakaDate = DateOnly.FromDateTime(Now.ToOffset(dhakaOffset).DateTime).AddDays(daysAhead);
        return new DateTimeOffset(dhakaDate.ToDateTime(new TimeOnly(10, 0)), dhakaOffset).ToUniversalTime();
    }

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    protected static async Task<HttpResponseMessage> ExpectAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    // ── Customers ───────────────────────────────────────────────────────────────────────────────────
    protected async Task<PortalCustomer> RegisterCustomerAsync(string fullName = "Portal Customer", WebApplicationFactory<Program>? host = null)
    {
        var client = ClientOn(host ?? Factory);
        var captcha = await client.PostAsync("/api/captcha", null);
        captcha.EnsureSuccessStatusCode();
        var captchaId = (await ReadAsync(captcha)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName, mobileNumber = mobile, password = CustomerPassword, captchaId, captchaAnswer = "TEST1" }, Json);
        await ExpectAsync(HttpStatusCode.Created, register);
        var customerId = (await ReadAsync(register)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = CustomerPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        return new PortalCustomer(client, customerId, mobile, fullName);
    }

    protected sealed record PortalCustomer(HttpClient Client, Guid CustomerId, string Mobile, string FullName);

    /// <summary>A customer registers one of their own cats through the Cat Profiles endpoint (the owner id is required by the contract and forced to the caller).</summary>
    protected static async Task<Guid> RegisterCatAsync(PortalCustomer customer, string name)
    {
        var response = await customer.Client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = customer.CustomerId, name, species = "Cat", sex = "Female",
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2), dateOfBirthPrecision = "Exact",
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("catId").GetGuid();
    }

    // ── Staff ───────────────────────────────────────────────────────────────────────────────────────
    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Portal Staff " + NewCode(), MobileNumber.Create(mobile), null, roles,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        staff.SetPassword(new PasswordSetDto(hasher.Hash(StaffPassword), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, mobile, StaffPassword);
    }

    protected async Task<(HttpClient Client, Guid StaffAccountId)> SignInNewStaffAsync(string role, WebApplicationFactory<Program>? host = null)
    {
        var staff = await SeedStaffAsync(role);
        var client = ClientOn(host ?? Factory);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = staff.Mobile, password = staff.Password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, staff.StaffAccountId);
    }

    protected async Task MarkCatDeceasedAsync(Guid catId)
    {
        var (admin, _) = await SignInNewStaffAsync(StaffRoles.Administrator);
        var response = await admin.PostAsJsonAsync($"/api/cats/{catId}/deceased", new { deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow) }, Json);
        await ExpectAsync(HttpStatusCode.OK, response);
    }

    // ── A bookable clinic branch (Appointments' own admin endpoints) ────────────────────────────────
    /// <summary><see cref="Admin"/> is assigned to the branch and sends it as the active branch, so staff actions on it are allowed.</summary>
    protected sealed record ClinicContext(
        Guid BranchId, string BranchCode, Guid ServiceId, Guid VetStaffAccountId, string VetMobile, string VetPassword, HttpClient Admin, Guid AdminStaffId);

    protected async Task<ClinicContext> SeedBookableBranchAsync()
    {
        var adminAccount = await SeedStaffAsync(StaffRoles.Administrator);
        var admin = ClientOn(Factory);
        var adminSignIn = await admin.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = adminAccount.Mobile, password = adminAccount.Password }, Json);
        await ExpectAsync(HttpStatusCode.OK, adminSignIn);
        admin.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(adminSignIn, "pc_csrf"));

        var code = NewCode();
        var createBranch = await admin.PostAsJsonAsync("/api/admin/branches", new { code, name = "Portal Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        await ExpectAsync(HttpStatusCode.Created, createBranch);
        var branchId = (await ReadAsync(createBranch)).GetProperty("branchId").GetGuid();

        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/staff/{adminAccount.StaffAccountId}/branches",
            new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json));
        admin.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());

        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours",
            new { intervals = days.Select(d => new { dayOfWeek = d, opens = "00:00", closes = "23:45" }).ToArray() }, Json));
        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms",
            new { name = "Room " + NewCode(), kind = "Consultation" }, Json));

        var vet = await SeedStaffAsync(StaffRoles.Vet);
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches",
            new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern",
            new { slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray() }, Json));

        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Standard " + NewCode() }, Json);
        await ExpectAsync(HttpStatusCode.Created, taxClass);
        var taxClassId = (await ReadAsync(taxClass)).GetProperty("taxClassId").GetGuid();

        var service = await admin.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode(), name = "Vaccination " + NewCode(), description = "Portal test service", category = "Consultation",
            durationMinutes = 30, qualifiedRoleIds = new[] { StaffRoles.Vet }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true,
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, service);
        var serviceId = (await ReadAsync(service)).GetProperty("serviceId").GetGuid();

        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices",
            new { amount = "500.00", currencyCode = "BDT", effectiveFrom = Now }, Json));

        return new ClinicContext(branchId, code, serviceId, vet.StaffAccountId, vet.Mobile, vet.Password, admin, adminAccount.StaffAccountId);
    }

    protected async Task<HttpClient> SignInStaffOnBranchAsync(string mobile, string password, Guid branchId)
    {
        var client = ClientOn(Factory);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());
        return client;
    }

    protected async Task<HttpClient> AddStaffAsync(ClinicContext clinic, string role)
    {
        var staff = await SeedStaffAsync(role);
        await ExpectAsync(HttpStatusCode.NoContent, await clinic.Admin.PutAsJsonAsync($"/api/admin/staff/{staff.StaffAccountId}/branches",
            new { branchIds = new[] { clinic.BranchId }, primaryBranchId = clinic.BranchId }, Json));
        return await SignInStaffOnBranchAsync(staff.Mobile, staff.Password, clinic.BranchId);
    }

    // ── Visits (Consultation's own staff endpoints) ─────────────────────────────────────────────────
    /// <summary>A walk-in checked in by the administrator and a visit opened by the nurse -- a DRAFT visit. Returns the visit id.</summary>
    protected async Task<(Guid VisitId, HttpClient Vet)> OpenDraftVisitAsync(ClinicContext clinic, Guid customerId, Guid catId)
    {
        var nurse = await AddStaffAsync(clinic, StaffRoles.Nurse);
        var vet = await SignInStaffOnBranchAsync(clinic.VetMobile, clinic.VetPassword, clinic.BranchId);

        var walkIn = await clinic.Admin.PostAsJsonAsync("/api/staff/walk-ins", new { customerId, catId, serviceId = clinic.ServiceId, triagePriority = "Routine" }, Json);
        await ExpectAsync(HttpStatusCode.Created, walkIn);
        var appointmentId = (await ReadAsync(walkIn)).GetProperty("appointmentId").GetGuid();

        var open = await nurse.PostAsJsonAsync("/api/staff/visits", new { appointmentId }, Json);
        Assert.True(open.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await open.Content.ReadAsStringAsync());
        var visitId = (await ReadAsync(open)).GetProperty("visitId").GetGuid();
        return (visitId, vet);
    }

    /// <summary>A visit the vet has written up and finalised.</summary>
    protected async Task<Guid> FinaliseVisitAsync(ClinicContext clinic, Guid customerId, Guid catId, string dischargeInstructions = "Keep the ear dry.")
    {
        var (visitId, vet) = await OpenDraftVisitAsync(clinic, customerId, catId);

        var code = "T-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var createCode = await vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code, name = "Otitis externa " + code }, Json);
        await ExpectAsync(HttpStatusCode.Created, createCode);
        var codeId = (await ReadAsync(createCode)).GetProperty("diagnosisCodeId").GetGuid();

        var version = (await ReadAsync(await vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        await ExpectAsync(HttpStatusCode.OK, await vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Otitis externa", plan = "Ear drops", dischargeInstructions,
            problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } },
        }, Json));

        version = (await ReadAsync(await vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        await ExpectAsync(HttpStatusCode.NoContent, await vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", new { expectedVersion = version }, Json));
        return visitId;
    }

    // ── Billing (the owning module's port and staff endpoints) ──────────────────────────────────────
    /// <summary>Gives the clinic's branch a billing profile and a zero-rated tax class (a rate effective now is seeded through the
    /// repository: the admin API refuses back-dating).</summary>
    protected async Task<Guid> PrepareBillingAsync(ClinicContext clinic)
    {
        await ExpectAsync(HttpStatusCode.NoContent, await clinic.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{clinic.BranchId}", new
        {
            legalName = "PurrCare Test Ltd " + clinic.BranchCode, taxRegistrationNumber = "BIN-" + clinic.BranchCode,
            addressLines = new[] { "1 Test Road", "Dhaka 1212" }, phone = "01700000000", email = "billing@example.test",
            invoiceFooter = "Thank you",
            bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json));

        var taxClass = await clinic.Admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Zero " + NewCode() }, Json);
        await ExpectAsync(HttpStatusCode.Created, taxClass);
        var taxClassId = (await ReadAsync(taxClass)).GetProperty("taxClassId").GetGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<TaxRateAggregate>>();
        var rate = new TaxRateAggregate(Guid.NewGuid());
        var effectiveFrom = Now.AddDays(-30);
        rate.Create(new TaxRateCreateDto(taxClassId, 0m, effectiveFrom, effectiveFrom.AddDays(-1), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
        return taxClassId;
    }

    /// <summary>Records one product charge through Billing's own port (what Orders will do) and issues it as an invoice through Billing's staff
    /// endpoint. Returns the invoice id.</summary>
    protected async Task<Guid> IssueInvoiceAsync(ClinicContext clinic, Guid taxClassId, Guid customerId, decimal amount)
    {
        var request = new CatEcommerce.Domain.Models.ChargeRequestDto(
            customerId, clinic.BranchId, null, CatEcommerce.Shared.Common.Enums.BusinessLine.Shop,
            CatEcommerce.Shared.Common.Enums.ItemKind.Product, CatEcommerce.Shared.Common.Enums.ChargeSourceKind.Order, Guid.NewGuid(), "main", null, null,
            "Portal test item", 1m, new Money(amount, "BDT"), new Money(0m, "BDT"), taxClassId, DateOnly.FromDateTime(Now.UtcDateTime));

        Guid chargeId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            chargeId = await scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>().RecordAsync(request);
            await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        }

        var issue = await clinic.Admin.PostAsJsonAsync("/api/staff/billing/invoices/issue-from-charges", new { customerId, selector = new { chargeIds = new[] { chargeId } } }, Json);
        await ExpectAsync(HttpStatusCode.Created, issue);
        return (await ReadAsync(issue)).GetProperty("invoiceId").GetGuid();
    }

    /// <summary>The customer holds and confirms a slot through the customer endpoints.</summary>
    protected static async Task<Guid> BookConfirmedAppointmentAsync(HttpClient customerClient, ClinicContext clinic, Guid catId, DateTimeOffset startsOn)
    {
        var hold = await customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds",
            new { branchId = clinic.BranchId, serviceId = clinic.ServiceId, catId, startsOn, performerId = clinic.VetStaffAccountId }, Json);
        await ExpectAsync(HttpStatusCode.Created, hold);
        var appointmentId = (await ReadAsync(hold)).GetProperty("appointmentId").GetGuid();

        await ExpectAsync(HttpStatusCode.OK, await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/confirm", new { }, Json));
        return appointmentId;
    }

    // ── The dashboard ───────────────────────────────────────────────────────────────────────────────
    protected static async Task<JsonElement> GetDashboardAsync(HttpClient customerClient)
    {
        var response = await customerClient.GetAsync("/api/portal/dashboard");
        await ExpectAsync(HttpStatusCode.OK, response);
        return await ReadAsync(response);
    }
}
