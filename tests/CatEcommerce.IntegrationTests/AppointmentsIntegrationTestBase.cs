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
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-APT-001 §16.1 -- every test creates its own branch, rooms and rostered staff. Helpers mint
/// fresh customers, staff and cats per test, and build a fully bookable branch (open and rostered
/// every day of the week, one Consultation room, one Vet-qualified service priced from the given
/// clock) so a test only has to pick a start time within the day it built.
/// </summary>
public abstract class AppointmentsIntegrationTestBase(AppointmentsApiFactory factory)
    : CatEcommerceIntegrationTestBase<AppointmentsApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        var rawValue = header[(cookieName.Length + 1)..].Split(';')[0];
        return Uri.UnescapeDataString(rawValue);
    }

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

    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Test Cat")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby",
            DateOnly.FromDateTime(DateTime.UtcNow),
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>A fully bookable branch: open and rostered every day of the week (00:00-23:45,
    /// Asia/Dhaka), one Consultation room, one Vet-qualified 30-minute service priced as of
    /// <paramref name="now"/> (a price's <c>EffectiveFrom</c> must be &gt;= now at creation and
    /// &lt;= now whenever a booking reads it -- only reliable with a controlled clock).</summary>
    protected sealed record ClinicTestContext(Guid BranchId, Guid RoomId, Guid VetStaffAccountId, string VetMobile, string VetPassword, Guid ServiceId);

    protected async Task<ClinicTestContext> SeedFullyBookableBranchAsync(HttpClient adminClient, DateTimeOffset now)
    {
        var code = NewCode();
        var createBranch = await adminClient.PostAsJsonAsync("/api/admin/branches", new { code, name = "Test Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        Assert.True(createBranch.StatusCode == HttpStatusCode.Created, await createBranch.Content.ReadAsStringAsync());
        var branchId = (await createBranch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var intervals = days.Select(d => new { dayOfWeek = d, opens = "00:00", closes = "23:45" }).ToArray();
        var setHours = await adminClient.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours", new { intervals }, Json);
        Assert.True(setHours.StatusCode == HttpStatusCode.NoContent, await setHours.Content.ReadAsStringAsync());

        var createRoom = await adminClient.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Room " + NewCode(), kind = "Consultation" }, Json);
        Assert.True(createRoom.StatusCode == HttpStatusCode.Created, await createRoom.Content.ReadAsStringAsync());
        var roomId = (await createRoom.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("roomId").GetGuid();

        var vet = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());

        var slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray();
        var setPattern = await adminClient.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new { slots }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());

        var createTaxClass = await adminClient.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Standard " + NewCode() }, Json);
        Assert.True(createTaxClass.StatusCode == HttpStatusCode.Created, await createTaxClass.Content.ReadAsStringAsync());
        var taxClassId = (await createTaxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();

        var createService = await adminClient.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode(), name = "Consultation " + NewCode(), description = "Test service", category = "Consultation",
            durationMinutes = 30, qualifiedRoleIds = new[] { CatEcommerce.Shared.Common.Constants.StaffRoles.Vet },
            requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        Assert.True(createService.StatusCode == HttpStatusCode.Created, await createService.Content.ReadAsStringAsync());
        var serviceId = (await createService.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var setPrice = await adminClient.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new
        {
            amount = "500.00", currencyCode = "BDT", effectiveFrom = now
        }, Json);
        Assert.True(setPrice.StatusCode == HttpStatusCode.Created, await setPrice.Content.ReadAsStringAsync());

        return new ClinicTestContext(branchId, roomId, vet.StaffAccountId, vet.Mobile, vet.Password, serviceId);
    }

    protected static HttpClient CreateClientOn(WebApplicationFactory<Program> apiFactory) =>
        apiFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });

    /// <summary>Staff sessions have a 30-minute idle timeout (HO-ACC-001); jumping a shared fake
    /// clock forward by more than that makes an already-signed-in session look idle. Re-sign-in
    /// (refreshing the session cookie via <c>HandleCookies</c>) immediately after every jump.</summary>
    protected static async Task ReauthenticateAsync(HttpClient client, string mobile, string password)
    {
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    protected async Task<(HttpClient AdminClient, ClinicTestContext Context, (Guid StaffAccountId, string Mobile, string Password) Admin)> SeedBranchWithAdminAssignedAsync(
        WebApplicationFactory<Program> apiFactory, DateTimeOffset now)
    {
        var admin = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Administrator);
        var adminClient = CreateClientOn(apiFactory);
        var signIn = await adminClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        adminClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var context = await SeedFullyBookableBranchAsync(adminClient, now);

        // The admin performing staff actions must itself resolve to this branch.
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{admin.StaffAccountId}/branches", new { branchIds = new[] { context.BranchId }, primaryBranchId = context.BranchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
        adminClient.DefaultRequestHeaders.Remove(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName);
        adminClient.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, context.BranchId.ToString());

        return (adminClient, context, admin);
    }

    /// <summary>By-id staff actions are scoped to the caller's active branch (backlog #8), so an admin
    /// that acts on a branch's appointments must be assigned to it and name it in X-Branch-Id. The
    /// first client performs the assignment; every client gets the header.</summary>
    protected static async Task ActivateBranchForAdminAsync(Guid adminStaffAccountId, Guid branchId, params HttpClient[] adminClients)
    {
        var assign = await adminClients[0].PutAsJsonAsync($"/api/admin/staff/{adminStaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());

        foreach (var client in adminClients)
        {
            client.DefaultRequestHeaders.Remove(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName);
            client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());
        }
    }

    protected static async Task<(HttpClient Client, Guid CustomerId, string Mobile)> RegisterAndSignInCustomerOnAsync(WebApplicationFactory<Program> apiFactory)
    {
        var client = CreateClientOn(apiFactory);
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaBody = await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var captchaId = captchaBody.GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Test Customer", mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1" }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var customerId = (await register.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        return (client, customerId, mobile);
    }

    protected static async Task<Guid> BookConfirmedAppointmentAsync(HttpClient customerClient, ClinicTestContext context, Guid catId, DateTimeOffset startsOn, Guid? performerId = null)
    {
        var hold = await customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId, startsOn, performerId = performerId ?? context.VetStaffAccountId }, Json);
        Assert.True(hold.StatusCode == HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var appointmentId = (await hold.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();

        var confirm = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/confirm", new { }, Json);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        return appointmentId;
    }

    /// <summary>A second room of the same Consultation kind at the given branch, for move-conflict
    /// tests that need two distinct rooms.</summary>
    protected static async Task<Guid> CreateRoomAsync(HttpClient adminClient, Guid branchId, string kind = "Consultation")
    {
        var createRoom = await adminClient.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Room " + NewCode(), kind }, Json);
        Assert.True(createRoom.StatusCode == HttpStatusCode.Created, await createRoom.Content.ReadAsStringAsync());
        return (await createRoom.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("roomId").GetGuid();
    }

    /// <summary>A second vet, assigned and rostered at the given branch every day 00:00-23:45 --
    /// for move/queue tests that need two distinct performers.</summary>
    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> CreateAndRosterVetAsync(HttpClient adminClient, Guid branchId)
    {
        var vet = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());

        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray();
        var setPattern = await adminClient.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new { slots }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());

        return vet;
    }

    /// <summary>Runs one <c>ClinicWorkCycle</c> pass (close past-date entries, lapse expired offers,
    /// offer freed slots -- §8.11/§12.2) in a fresh scope, the same shape
    /// <c>NotificationsIntegrationTestBase.RunDeliveryPassAsync</c> uses for
    /// <c>NotificationWorkCycle</c>: driven directly, since the hosted worker is disabled in
    /// Testing.</summary>
    protected static async Task RunClinicWorkCycleAsync(WebApplicationFactory<Program> apiFactory)
    {
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var workCycle = scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.ClinicWorkCycle>();
        await workCycle.RunAsync();
    }

    /// <summary>Narrows the given vet's roster to exactly one bookable window that day (§16.1's
    /// "fill a day" setup, without booking dozens of slots): with the default 15-minute grid and a
    /// 30-minute service, a 30-minute shift admits exactly one start.</summary>
    protected static async Task NarrowVetToOneSlotADayAsync(HttpClient adminClient, Guid branchId, Guid vetId, string starts, string ends)
    {
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var slots = days.Select(d => new { dayOfWeek = d, starts, ends }).ToArray();
        var setPattern = await adminClient.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vetId}/pattern", new { slots }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());
    }

    /// <summary>ACCEPTANCE gate fix (2026-09-28, A12/A13): runs one HO-NTF-001 delivery pass (expand
    /// pending requests, deliver due notifications, redact due secrets) in a fresh scope -- the same
    /// shape <c>NotificationsIntegrationTestBase.RunDeliveryPassAsync</c> uses, driven directly here
    /// because <see cref="AppointmentsIntegrationTestBase"/> does not itself inherit that base (it
    /// inherits <see cref="CatEcommerceIntegrationTestBase{TFactory}"/> for <c>AppointmentsApiFactory</c>
    /// instead). Both factories host the same single <c>Program</c>, so the same
    /// <c>NotificationWorkCycle</c> service resolves here unchanged.</summary>
    protected static async Task RunNotificationDeliveryPassAsync(WebApplicationFactory<Program> apiFactory)
    {
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var workCycle = scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.NotificationWorkCycle>();
        await workCycle.RunDeliveryPassAsync();
    }

    /// <summary>
    /// ACCEPTANCE gate fix (2026-09-29, HIGH -- test-isolation defect, not a product defect).
    /// <see cref="RunNotificationDeliveryPassAsync"/> alone assumed a SINGLE pass would always reach
    /// this test's own row. It does not: <c>NotificationWorkCycle.RunDeliveryPassAsync</c> expands
    /// only the oldest <c>Notifications:Worker:BatchSize</c> (50) pending requests, and delivers only
    /// the oldest 50 due notifications, across the WHOLE shared, never-truncated §16.1 database --
    /// other Appointments test classes outside <c>[Collection("Notifications")]</c> book appointments
    /// (which enqueue <c>appointment.confirmed</c>) and never run a delivery pass themselves, so their
    /// Pending rows accumulate, most sharing this test's own fake <c>RequestedOn</c> (an arbitrary tie
    /// order). Once 50 or more foreign rows are pending, a test's own row can miss its one pass
    /// entirely -- reproduced on demand (ACCEPTANCE second pass HIGH finding).
    ///
    /// This drains passes -- bounded by <paramref name="maxPasses"/> -- until <paramref name="isDone"/>
    /// (checked via a fresh, short-lived scope each time, so it never sees stale tracked state) is
    /// true, or fails loudly if it never is. This is SAFE for a "still held" assertion (e.g. I-D7's
    /// reminder deferred for quiet hours): looping more passes at the SAME fake "now" can only expand
    /// more of the backlog and deliver more notifications that are ALREADY due at that instant --
    /// <c>NotificationWorkQueue</c>'s own <c>SendAfter</c>/<c>NotBefore</c> filters mean it can never
    /// pull forward something that is not yet due, no matter how many extra passes run. A caller that
    /// wants to assert "still held" must therefore never use "reaches Sent" as <paramref name="isDone"/>
    /// -- assert the held state SEPARATELY, after looping only until the row has been (re-)expanded
    /// (reached Queued), never past that.
    /// </summary>
    protected static async Task RunNotificationDeliveryUntilAsync(
        WebApplicationFactory<Program> apiFactory, Func<CatEcommerceDbContext, Task<bool>> isDone, int maxPasses = 200)
    {
        for (var pass = 0; pass < maxPasses; pass++)
        {
            await RunNotificationDeliveryPassAsync(apiFactory);

            await using var scope = apiFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            if (await isDone(db)) return;
        }

        Assert.Fail($"RunNotificationDeliveryUntilAsync did not reach the expected state within {maxPasses} passes -- " +
            "either the assertion is wrong, or the shared test database's backlog is now larger than this bound covers.");
    }
}
