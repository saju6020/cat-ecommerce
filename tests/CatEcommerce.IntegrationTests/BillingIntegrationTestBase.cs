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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-BIL-001 §16.1 -- every test creates its own branch (so its invoice series, cash-ups and statements are
/// its own), its own billing profile, its own tax class and rates, and its own customers. Invoice numbers are
/// never asserted as literals -- only as <c>INV-{thatBranchCode}-…</c> and contiguous within the series.
/// Producers that do not exist yet (Shop, Boarding, Pharmacy) are simulated by resolving
/// <c>IBillableChargeRecorder</c> in a DI scope and saving -- the same port HO-ORD-001 and HO-BRD-001 will call.
/// </summary>
public abstract class BillingIntegrationTestBase(BillingApiFactory factory)
    : CatEcommerceIntegrationTestBase<BillingApiFactory>(factory)
{
    protected const string StaffPassword = "staffpassword1";

    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true,
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewCode() => "B" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    /// <summary>The branch-local date (Asia/Dhaka, UTC+6, no DST) at the factory's clock.</summary>
    protected DateOnly Today => DateOnly.FromDateTime(Now.ToOffset(TimeSpan.FromHours(6)).DateTime);

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    protected static async Task<string> BodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    /// <summary>Asserts the status and, on a mismatch, fails with the response body so a failing test explains itself.</summary>
    protected static async Task<HttpResponseMessage> ExpectAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {await BodyAsync(response)}");
        return response;
    }

    protected static async Task<string[]> ErrorCodesAsync(HttpResponseMessage response)
    {
        var body = await ReadAsync(response);
        if (!body.TryGetProperty("errors", out var errors)) return [];
        return errors.EnumerateArray().Select(e => e.GetProperty("errorCode").GetString()!).ToArray();
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
            "Billing Staff " + NewCode(), MobileNumber.Create(mobile), null, roles,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        staff.SetPassword(new PasswordSetDto(hasher.Hash(StaffPassword), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, mobile, StaffPassword);
    }

    protected async Task<HttpClient> SignInStaffAsync(string mobile, string password, Guid? branchId = null)
    {
        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        if (branchId is { } id)
            client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, id.ToString());
        return client;
    }

    // ── A billing-ready branch ──────────────────────────────────────────────────────────────────────
    /// <summary>One branch with a billing profile, a tax class at <see cref="StandardRate"/> and a zero-rated
    /// class, an administrator assigned to it, and the active-branch header already on <see cref="Admin"/>.</summary>
    protected sealed record BillingContext(
        Guid BranchId, string BranchCode, Guid TaxClassId, Guid ZeroTaxClassId, HttpClient Admin, Guid AdminStaffId, string TimeZoneId, string AdminMobile = "", string AdminPassword = "");

    protected const decimal StandardRate = 15m;

    protected async Task<BillingContext> CreateBillingContextAsync(bool withProfile = true, decimal rate = StandardRate)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var code = NewCode();
        var createBranch = await adminClient.PostAsJsonAsync("/api/admin/branches", new { code, name = "Billing Test " + code, timeZoneId = "Asia/Dhaka" }, Json);
        await ExpectAsync(HttpStatusCode.Created, createBranch);
        var branchId = (await ReadAsync(createBranch)).GetProperty("branchId").GetGuid();

        await AssignAsync(adminClient, admin.StaffAccountId, branchId);
        adminClient.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());

        var taxClassId = await CreateTaxClassAsync(adminClient, rate, Now.AddDays(-30));
        var zeroClassId = await CreateTaxClassAsync(adminClient, 0m, Now.AddDays(-30));

        var context = new BillingContext(branchId, code, taxClassId, zeroClassId, adminClient, admin.StaffAccountId, "Asia/Dhaka", admin.Mobile, admin.Password);
        if (withProfile) await SetProfileAsync(context);
        return context;
    }

    protected async Task AssignAsync(HttpClient adminClient, Guid staffAccountId, Guid branchId) =>
        await ExpectAsync(HttpStatusCode.NoContent, await adminClient.PutAsJsonAsync(
            $"/api/admin/staff/{staffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json));

    protected async Task SetProfileAsync(BillingContext context, string? legalName = null, string? taxNumber = null) =>
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{context.BranchId}", new
        {
            legalName = legalName ?? "PurrCare Test Ltd " + context.BranchCode,
            taxRegistrationNumber = taxNumber ?? "BIN-" + context.BranchCode,
            addressLines = new[] { "1 Test Road", "Dhaka 1212" },
            phone = "01700000000",
            email = "billing@example.test",
            invoiceFooter = "Thank you for choosing PurrCare",
            bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json));

    /// <summary>Creates a tax class through the API and seeds its first rate directly -- the API refuses a rate
    /// effective in the past ("no back-dating", HO-ADM-001), and a test needs one already in force.</summary>
    protected async Task<Guid> CreateTaxClassAsync(HttpClient adminClient, decimal ratePercent, DateTimeOffset effectiveFrom)
    {
        var response = await adminClient.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Tax " + NewCode() }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        var taxClassId = (await ReadAsync(response)).GetProperty("taxClassId").GetGuid();

        await SeedTaxRateAsync(taxClassId, ratePercent, effectiveFrom);
        return taxClassId;
    }

    protected async Task SeedTaxRateAsync(Guid taxClassId, decimal ratePercent, DateTimeOffset effectiveFrom)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<TaxRateAggregate>>();
        var rate = new TaxRateAggregate(Guid.NewGuid());
        rate.Create(new TaxRateCreateDto(taxClassId, ratePercent, effectiveFrom, effectiveFrom.AddDays(-1), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
    }

    /// <summary>A staff member of <paramref name="role"/> assigned to the context's branch and signed in.</summary>
    protected async Task<(HttpClient Client, Guid StaffAccountId)> StaffClientAsync(BillingContext context, string role)
    {
        var staff = await SeedStaffAsync(role);
        await AssignAsync(context.Admin, staff.StaffAccountId, context.BranchId);
        return (await SignInStaffAsync(staff.Mobile, staff.Password, context.BranchId), staff.StaffAccountId);
    }

    // ── Customers ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>A walk-in customer (no password) -- enough to owe and pay money.</summary>
    protected async Task<Guid> SeedCustomerAsync(BillingContext context, string? fullName = null)
    {
        var response = await context.Admin.PostAsJsonAsync("/api/staff/customers", new { fullName = fullName ?? "Billing Customer " + NewCode(), mobileNumber = NewMobile() }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("customerId").GetGuid();
    }

    /// <summary>A registered customer signed in through the real cookie path (for the customer-facing endpoints).</summary>
    protected async Task<(HttpClient Client, Guid CustomerId)> RegisterAndSignInCustomerAsync()
    {
        var client = CreateClient();
        var captcha = await client.PostAsync("/api/captcha", null);
        captcha.EnsureSuccessStatusCode();
        var captchaId = (await ReadAsync(captcha)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Portal Customer " + NewCode(), mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1" }, Json);
        await ExpectAsync(HttpStatusCode.Created, register);
        var customerId = (await ReadAsync(register)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, customerId);
    }

    // ── Direct database access (for assertions the API does not expose) ────────────────────────────────
    protected async Task<T> WithDbAsync<T>(Func<CatEcommerceDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>());
    }

    protected async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected Task<int> CountAsync<TAggregate>(System.Linq.Expressions.Expression<Func<TAggregate, bool>> predicate) where TAggregate : AggregateRoot =>
        WithDbAsync(db => db.Set<TAggregate>().AsNoTracking().CountAsync(predicate));

    // ── Clinic: a branch where an appointment can be walked in, started and completed ───────────────────
    protected sealed record ClinicSetup(Guid ServiceId, Guid VetStaffAccountId, Guid RoomId, decimal Price);

    /// <summary>Opens the context's branch every day, adds one consultation room, a rostered vet and a service
    /// priced from the fake clock's "now" (a price must be effective at or after "now" when created, and in force
    /// when read -- only reliable with a controlled clock).</summary>
    protected async Task<ClinicSetup> SeedClinicAsync(BillingContext context, decimal price = 800m, Guid? taxClassId = null)
    {
        var admin = context.Admin;
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/branches/{context.BranchId}/hours",
            new { intervals = days.Select(d => new { dayOfWeek = d, opens = "00:00", closes = "23:45" }).ToArray() }, Json));

        var room = await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/admin/branches/{context.BranchId}/rooms",
            new { name = "Room " + NewCode(), kind = "Consultation" }, Json));
        var roomId = (await ReadAsync(room)).GetProperty("roomId").GetGuid();

        var vet = await SeedStaffAsync(StaffRoles.Vet);
        await AssignAsync(admin, vet.StaffAccountId, context.BranchId);
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/admin/branches/{context.BranchId}/rosters/{vet.StaffAccountId}/pattern",
            new { slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray() }, Json));

        var service = await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode(), name = "Consultation " + NewCode(), description = "Billing test service", category = "Consultation",
            durationMinutes = 30, qualifiedRoleIds = new[] { StaffRoles.Vet }, requiredRoomKind = "Consultation",
            taxClassId = taxClassId ?? context.TaxClassId, isBookableOnline = true,
        }, Json));
        var serviceId = (await ReadAsync(service)).GetProperty("serviceId").GetGuid();

        await SetServicePriceAsync(context, serviceId, price);
        return new ClinicSetup(serviceId, vet.StaffAccountId, roomId, price);
    }

    protected async Task SetServicePriceAsync(BillingContext context, Guid serviceId, decimal price) =>
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices",
            new { amount = price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), currencyCode = "BDT", effectiveFrom = Now }, Json));

    protected async Task<Guid> SeedCatAsync(Guid ownerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatAggregate>>();

        var cat = new CatEcommerce.Domain.Aggregates.Cat.CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Cat " + NewCode(), "Cat", CatEcommerce.Shared.Common.Enums.CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            CatEcommerce.Shared.Common.Enums.DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(DateTime.UtcNow),
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>Registers a walk-in at the context's branch (the snapshot price is taken now) and returns its id.</summary>
    protected async Task<Guid> RegisterWalkInAsync(BillingContext context, ClinicSetup clinic, Guid customerId)
    {
        var catId = await SeedCatAsync(customerId);
        var response = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/walk-ins",
            new { customerId, catId, serviceId = clinic.ServiceId, triagePriority = "Routine" }, Json));
        return (await ReadAsync(response)).GetProperty("appointmentId").GetGuid();
    }

    protected async Task<HttpResponseMessage> CompleteAppointmentAsync(BillingContext context, ClinicSetup clinic, Guid appointmentId)
    {
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start", new { performerId = clinic.VetStaffAccountId, roomId = clinic.RoomId }, Json));
        return await context.Admin.PostAsync($"/api/staff/appointments/{appointmentId}/complete", null);
    }

    // ── Producers that do not exist yet, simulated through the real port (§16.1 rule 3) ──────────────────
    /// <summary>Records one charge exactly as HO-ORD-001 / HO-BRD-001 will: resolve <c>IBillableChargeRecorder</c> in a
    /// DI scope, enlist, and save. No test-only endpoint exists in the Api.</summary>
    protected async Task<Guid> RecordChargeAsync(
        BillingContext context, Guid customerId, decimal unitPrice = 100m, decimal quantity = 1m, decimal discount = 0m,
        CatEcommerce.Shared.Common.Enums.ItemKind? itemKind = null, CatEcommerce.Shared.Common.Enums.ChargeSourceKind? sourceKind = null,
        CatEcommerce.Shared.Common.Enums.BusinessLine? businessLine = null, Guid? sourceId = null, Guid? taxClassId = null, string sourceLineRef = "main",
        CatEcommerce.Shared.Common.Enums.BillingGroupKind? groupKind = null, Guid? groupId = null, DateOnly? serviceDate = null, string description = "Test item")
    {
        var source = sourceKind ?? CatEcommerce.Shared.Common.Enums.ChargeSourceKind.Order;
        var request = new ChargeRequestDto(
            customerId, context.BranchId, null, businessLine ?? ChargeRules.DefaultBusinessLine(source),
            itemKind ?? CatEcommerce.Shared.Common.Enums.ItemKind.Product, source, sourceId ?? Guid.NewGuid(), sourceLineRef, groupKind, groupId, description,
            quantity, new Money(unitPrice, "BDT"), new Money(discount, "BDT"), taxClassId ?? context.TaxClassId, serviceDate ?? Today);

        await using var scope = Factory.Services.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<CatEcommerce.Domain.DomainServices.IBillableChargeRecorder>();
        var id = await recorder.RecordAsync(request);
        await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        return id;
    }

    // ── Invoices ────────────────────────────────────────────────────────────────────────────────────────
    protected static decimal AmountOf(JsonElement money) =>
        decimal.Parse(money.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    protected static HttpRequestMessage WithKey(HttpMethod method, string url, object body, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: Json) };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    protected async Task<HttpResponseMessage> IssueFromChargesRawAsync(HttpClient client, Guid customerId, object selector, string? idempotencyKey = null) =>
        await client.SendAsync(WithKey(HttpMethod.Post, "/api/staff/billing/invoices/issue-from-charges", new { customerId, selector }, idempotencyKey));

    protected async Task<(Guid InvoiceId, string Number)> IssueFromChargesAsync(HttpClient client, Guid customerId, IEnumerable<Guid> chargeIds, string? idempotencyKey = null)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await IssueFromChargesRawAsync(client, customerId, new { chargeIds = chargeIds.ToArray() }, idempotencyKey));
        var body = await ReadAsync(response);
        return (body.GetProperty("invoiceId").GetGuid(), body.GetProperty("number").GetString()!);
    }

    /// <summary>Records one product charge of <paramref name="unitPrice"/> x <paramref name="quantity"/> through the port and issues it.</summary>
    protected async Task<(Guid InvoiceId, string Number, Guid ChargeId)> IssueSimpleInvoiceAsync(
        BillingContext context, Guid customerId, decimal unitPrice = 1000m, decimal quantity = 1m, Guid? taxClassId = null, string? idempotencyKey = null, HttpClient? client = null)
    {
        var chargeId = await RecordChargeAsync(context, customerId, unitPrice, quantity, taxClassId: taxClassId ?? context.ZeroTaxClassId);
        var (invoiceId, number) = await IssueFromChargesAsync(client ?? context.Admin, customerId, [chargeId], idempotencyKey);
        return (invoiceId, number, chargeId);
    }

    protected async Task<Guid> CreateDraftAsync(HttpClient client, Guid customerId, IEnumerable<Guid> chargeIds)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await client.PostAsJsonAsync("/api/staff/billing/invoices", new { customerId, chargeIds = chargeIds.ToArray() }, Json));
        return (await ReadAsync(response)).GetProperty("invoiceId").GetGuid();
    }

    protected async Task<JsonElement> GetInvoiceAsync(HttpClient client, Guid invoiceId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync($"/api/staff/billing/invoices/{invoiceId}")));

    /// <summary>A staff session idles out after 30 minutes of <i>fake</i> time; re-sign the context's administrator in after
    /// the clock jumped (the cookie jar keeps the new session cookie, the CSRF header is replaced).</summary>
    protected async Task ReSignInAsync(BillingContext context)
    {
        var signIn = await context.Admin.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = context.AdminMobile, password = context.AdminPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        context.Admin.DefaultRequestHeaders.Remove("X-CSRF-Token");
        context.Admin.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    /// <summary>One HO-NTF-001 delivery pass (expand pending requests, deliver due notifications), driven directly --
    /// the hosted worker is off in Testing.</summary>
    protected async Task RunDeliveryPassAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cycle = scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.NotificationWorkCycle>();
        await cycle.RunDeliveryPassAsync();
    }

    /// <summary>Delivery passes until <paramref name="delivered"/> holds. The notification queue is shared by every test in the database and a pass
    /// handles a bounded batch, so a message may need more than one pass to be reached when the queue is long.</summary>
    protected async Task RunDeliveryPassesUntilAsync(Func<Task<bool>> delivered, int maxPasses = 60)
    {
        for (var pass = 0; pass < maxPasses; pass++)
        {
            await RunDeliveryPassAsync();
            if (await delivered()) return;
        }

        // TEST gate (HO-BIL-001): this used to return silently here, so a negative assertion placed after it passed vacuously.
        throw new InvalidOperationException($"The condition did not hold after {maxPasses} delivery passes.");
    }

    /// <summary>The bodies of the signed-in customer's inbox.</summary>
    protected static async Task<List<string>> InboxBodiesAsync(HttpClient customerClient)
    {
        var inbox = await ReadAsync(await customerClient.GetAsync("/api/customers/me/inbox"));
        return inbox.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("body").GetString()!).ToList();
    }

    /// <summary>The financial-year-compact and sequence parts of a number issued at <paramref name="context"/>'s branch: <c>INV-{code}-2627-000123</c> -> (2627, 123).</summary>
    protected static (string FinancialYearCompact, long Sequence) ParseNumber(string number, string branchCode, string prefix = "INV")
    {
        var parts = number.Split('-');
        Assert.Equal(4, parts.Length);
        Assert.Equal(prefix, parts[0]);
        Assert.Equal(branchCode, parts[1]);
        return (parts[2], long.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Two customers that share a contact number are duplicates a merge will accept (HO-ACC-001).</summary>
    protected static async Task ShareContactNumberAsync(HttpClient customer, string sharedNumber)
    {
        var update = await customer.PutAsJsonAsync("/api/customers/me", new { fullName = "Merged Customer", additionalPhoneNumbers = new[] { sharedNumber } }, Json);
        await ExpectAsync(HttpStatusCode.NoContent, update);
    }

    // ── Payments ────────────────────────────────────────────────────────────────────────────────────────
    protected async Task<HttpResponseMessage> RecordPaymentRawAsync(
        HttpClient client, Guid customerId, string method, decimal amount, string? reference = null, object? collector = null, object? earmark = null,
        IEnumerable<(Guid InvoiceId, decimal Amount)>? allocations = null, string? idempotencyKey = null, DateOnly? receivedOn = null, string? notes = null) =>
        await client.SendAsync(WithKey(HttpMethod.Post, "/api/staff/billing/payments", new
        {
            customerId, method, amount, receivedOn = (receivedOn ?? Today).ToString("yyyy-MM-dd"), externalReference = reference, collector, earmark, notes,
            allocations = (allocations ?? []).Select(a => new { invoiceId = a.InvoiceId, amount = a.Amount }).ToArray(),
        }, idempotencyKey));

    /// <summary>Records a payment and expects <c>201</c>; returns the response body (<c>paymentId</c>, <c>status</c>, <c>applied</c>, …).</summary>
    protected async Task<JsonElement> RecordPaymentAsync(
        HttpClient client, Guid customerId, string method, decimal amount, string? reference = null, object? collector = null, object? earmark = null,
        IEnumerable<(Guid InvoiceId, decimal Amount)>? allocations = null, string? idempotencyKey = null)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await RecordPaymentRawAsync(client, customerId, method, amount, reference, collector, earmark, allocations, idempotencyKey));
        return await ReadAsync(response);
    }

    protected async Task<JsonElement> GetPaymentAsync(HttpClient client, Guid paymentId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync($"/api/staff/billing/payments/{paymentId}")));

    protected async Task<JsonElement> GetSummaryAsync(HttpClient client, Guid customerId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync($"/api/staff/billing/customers/{customerId}/summary")));
}
