using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 §16.1 -- the rules, as helpers. <b>Every test creates its own branch, delivery zone, SKUs and stock</b> through the upstream endpoints
/// (HO-ADM-001 branches and tax classes, HO-IMP-001 locations and goods receipts, HO-SHP-001 products and prices) and its own customers, so stock,
/// discount-usage and order-number assertions never see another test's rows. Discount codes are minted per test; idempotency keys are fresh UUIDs.
/// Shop, Inventory and Billing are the <b>real</b> implementations from their merged slices -- never fakes (only Pharmacy's gate is not merged).
/// The clock is <see cref="AdjustableTimeProvider"/>: fixtures built from <c>Now</c> are always in force.
/// </summary>
public abstract class OrderingIntegrationTestBase(OrderingApiFactory factory) : CatEcommerceIntegrationTestBase<OrderingApiFactory>(factory)
{
    protected const string StaffPassword = "staffpassword1";
    protected const string CustomerPassword = "correcthorsebattery";
    protected const decimal StandardRate = 15m;

    protected override HttpClient CreateClient() => CreateClientOn(Factory);

    protected static HttpClient CreateClientOn(WebApplicationFactory<Program> apiFactory) =>
        apiFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewCode(string prefix = "T") => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    protected static string NewLot() => "LOT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    /// <summary>The branch-local date (Asia/Dhaka, UTC+6, no DST) at the factory's clock.</summary>
    protected DateOnly Today => DateOnly.FromDateTime(Now.ToOffset(TimeSpan.FromHours(6)).DateTime);

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Json);

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

    protected static async Task AssertErrorAsync(HttpResponseMessage response, string expectedCode, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        Assert.True(response.StatusCode == status, $"Expected {(int)status} but got {(int)response.StatusCode}: {await BodyAsync(response)}");
        Assert.Contains(expectedCode, await ErrorCodesAsync(response));
    }

    protected static decimal AmountOf(JsonElement money) =>
        decimal.Parse(money.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    // ── staff and branches ──────────────────────────────────────────────────────────────────────────
    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Ordering Staff " + NewCode(), MobileNumber.Create(mobile), null, roles, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
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
        if (branchId is { } id) UseBranch(client, id);
        return client;
    }

    protected static void UseBranch(HttpClient client, Guid branchId)
    {
        client.DefaultRequestHeaders.Remove(HttpContextActiveBranchAccessor.BranchHeaderName);
        client.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());
    }

    protected async Task AssignAsync(HttpClient adminClient, Guid staffAccountId, Guid branchId) =>
        await ExpectAsync(HttpStatusCode.NoContent, await adminClient.PutAsJsonAsync(
            $"/api/admin/staff/{staffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json));

    /// <summary>A test's own branch: an administrator assigned to it (the <see cref="Admin"/> client, branch header already set), two stock
    /// locations (a shop floor to sell from and the quarantine every branch needs) and a 15 % and a 0 % tax class.</summary>
    protected sealed record OrderingContext(
        Guid BranchId, string BranchCode, HttpClient Admin, Guid AdminStaffId, string AdminMobile, string AdminPassword,
        Guid ShopLocationId, Guid QuarantineLocationId, Guid TaxClassId, Guid ZeroTaxClassId);

    protected async Task<OrderingContext> CreateContextAsync(decimal rate = StandardRate)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var code = NewCode("B");
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Ordering Test " + code, timeZoneId = "Asia/Dhaka" }, Json);
        await ExpectAsync(HttpStatusCode.Created, create);
        var branchId = (await ReadAsync(create)).GetProperty("branchId").GetGuid();

        await AssignAsync(client, admin.StaffAccountId, branchId);
        UseBranch(client, branchId);

        var shop = await CreateLocationAsync(client, "ShopFloor");
        var quarantine = await CreateLocationAsync(client, "Quarantine");
        var taxClassId = await CreateTaxClassAsync(client, rate);
        var zeroClassId = await CreateTaxClassAsync(client, 0m);

        return new OrderingContext(branchId, code, client, admin.StaffAccountId, admin.Mobile, admin.Password, shop, quarantine, taxClassId, zeroClassId);
    }

    protected async Task<Guid> CreateLocationAsync(HttpClient admin, string kind)
    {
        var response = await admin.PostAsJsonAsync("/api/staff/inventory/locations", new { code = NewCode("L"), name = $"{kind} {Guid.NewGuid():N}"[..20], kind }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("locationId").GetGuid();
    }

    /// <summary>A staff member of <paramref name="role"/> assigned to the context's branch and signed in with the branch header.</summary>
    protected async Task<(HttpClient Client, Guid StaffAccountId)> StaffClientAsync(OrderingContext context, string role)
    {
        var staff = await SeedStaffAsync(role);
        await AssignAsync(context.Admin, staff.StaffAccountId, context.BranchId);
        return (await SignInStaffAsync(staff.Mobile, staff.Password, context.BranchId), staff.StaffAccountId);
    }

    /// <summary>Creates a tax class through the API and seeds its first rate directly -- the API refuses a rate effective in the past
    /// ("no back-dating", HO-ADM-001), and a test needs one already in force.</summary>
    protected async Task<Guid> CreateTaxClassAsync(HttpClient adminClient, decimal ratePercent)
    {
        var response = await adminClient.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX"), name = "Tax " + NewCode() }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        var taxClassId = (await ReadAsync(response)).GetProperty("taxClassId").GetGuid();

        await SeedTaxRateAsync(taxClassId, ratePercent, Now.AddDays(-30));
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

    // ── customers ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>A registered customer signed in through the real cookie path, CSRF header attached.</summary>
    protected async Task<(HttpClient Client, Guid CustomerId, string Mobile)> RegisterAndSignInCustomerAsync(WebApplicationFactory<Program>? apiFactory = null)
    {
        var client = CreateClientOn(apiFactory ?? Factory);
        var captcha = await client.PostAsync("/api/captcha", null);
        captcha.EnsureSuccessStatusCode();
        var captchaId = (await ReadAsync(captcha)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Ordering Customer " + NewCode(), mobileNumber = mobile, password = CustomerPassword, captchaId, captchaAnswer = "TEST1" }, Json);
        await ExpectAsync(HttpStatusCode.Created, register);
        var customerId = (await ReadAsync(register)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = CustomerPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, customerId, mobile);
    }

    protected async Task<Guid> AddAddressAsync(HttpClient customer, string district = "Dhaka", string? postalCode = "1212", string line1 = "House 1, Road 2")
    {
        var response = await customer.PostAsJsonAsync("/api/customers/me/addresses", new
        { label = "Home", line1, line2 = (string?)null, city = "Dhaka", district, postalCode, deliveryInstructions = (string?)null }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("addressId").GetGuid();
    }

    // ── catalogue and stock (through the real endpoints) ────────────────────────────────────────────
    protected sealed record OrderingSku(Guid ProductId, Guid SkuId, string Name, decimal Price, string? Barcode, IReadOnlyList<Guid> BatchIds);

    /// <summary>Mints a product and one SKU through HO-SHP-001's staff endpoints, prices it from <see cref="Now"/> (in force immediately), publishes
    /// it and receives <paramref name="stock"/> units into the context's shop floor through HO-IMP-001's goods receipt.</summary>
    protected async Task<OrderingSku> CreateSkuAsync(
        OrderingContext context, decimal price = 100m, decimal stock = 50m, Guid? taxClassId = null, bool food = false, bool prescription = false,
        string? barcode = null, string? name = null, DateOnly? expires = null, bool publish = true, bool onStorefront = false)
    {
        var admin = context.Admin;
        var productName = name ?? "Ordering product " + Guid.NewGuid().ToString("N")[..8];
        var category = food ? "CatFood" : "HealthAndGrooming";
        var tax = taxClassId ?? context.TaxClassId;
        var brand = "Brand-" + Guid.NewGuid().ToString("N")[..6];

        var create = await admin.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = productName, brand, category, taxClassId = tax, slug = "p-" + Guid.NewGuid().ToString("N") }, Json);
        await ExpectAsync(HttpStatusCode.Created, create);
        var productId = (await ReadAsync(create)).GetProperty("productId").GetGuid();

        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        { expectedVersion = await ProductVersionAsync(admin, productId), name = productName, brand, category, taxClassId = tax, countryOfOriginCode = food ? "DE" : null, tags = Array.Empty<string>(), healthFocus = Array.Empty<string>() }, Json));

        if (food)
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food",
                new { expectedVersion = await ProductVersionAsync(admin, productId), lifeStage = "Adult" }, Json));

        if (prescription)
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription",
                new { expectedVersion = await ProductVersionAsync(admin, productId), prescriptionRequired = true }, Json));

        var addSku = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await ProductVersionAsync(admin, productId), code = NewCode("SKU"), barcode }, Json);
        await ExpectAsync(HttpStatusCode.Created, addSku);
        var skuId = (await ReadAsync(addSku)).GetProperty("skuId").GetGuid();

        await SchedulePriceAsync(admin, skuId, price);

        var batches = new List<Guid>();
        if (stock > 0)
        {
            var needsExpiry = food;
            batches.Add(await ReceiveAsync(admin, skuId, context.ShopLocationId, stock, needsExpiry ? expires ?? UtcToday.AddYears(1) : expires));
        }

        if (onStorefront && stock > 0) await StockAtStorefrontAsync(skuId, stock, food ? expires ?? UtcToday.AddYears(1) : expires);

        if (publish)
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
                new { expectedVersion = await ProductVersionAsync(admin, productId) }, Json));

        return new OrderingSku(productId, skuId, productName, price, barcode, batches);
    }

    protected DateOnly UtcToday => DateOnly.FromDateTime(Now.UtcDateTime);

    protected async Task<int> ProductVersionAsync(HttpClient staff, Guid productId) =>
        (await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json)).GetProperty("version").GetInt32();

    protected async Task SchedulePriceAsync(HttpClient admin, Guid skuId, decimal price) =>
        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices",
            new { amount = price, currencyCode = "BDT", effectiveFrom = Now }, Json));

    /// <summary>Receives <paramref name="quantity"/> into a new batch at <paramref name="locationId"/> and returns the batch id.</summary>
    protected async Task<Guid> ReceiveAsync(HttpClient client, Guid skuId, Guid locationId, decimal quantity, DateOnly? expires = null, string? lot = null)
    {
        var response = await client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new
        {
            lines = new[]
            {
                new
                {
                    skuId, locationId, lotNumber = lot ?? NewLot(), expiresOn = expires, quantityAccepted = quantity, quantityRejected = 0m, rejectionReason = (string?)null,
                    purchaseOrderLineId = (Guid?)null, unitCostAmount = (decimal?)5m, unitCostCurrency = (string?)"BDT", holdForInspection = false, exchangeRateToBase = (decimal?)null
                }
            }
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("batchIds").EnumerateArray().Select(b => b.GetGuid()).Single();
    }


    // ── policies (the checkout acceptance half, I-D13) ─────────────────────────────────────────────
    /// <summary>Accepts every policy version the customer still owes (through HO-ADM-001's endpoint). The shared database holds other tests' RequiresAcceptance
    /// policies, so every checkout test accepts what is pending first -- exactly what the storefront does before a customer can place an order.</summary>
    protected async Task AcceptPendingPoliciesAsync(HttpClient customer)
    {
        var pending = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await customer.GetAsync("/api/customers/me/policy-acceptances/pending")));
        foreach (var policy in pending.EnumerateArray())
            await ExpectAsync(HttpStatusCode.NoContent, await customer.PostAsJsonAsync("/api/customers/me/policy-acceptances",
                new { policyKey = policy.GetProperty("policyKey").GetString(), versionNumber = policy.GetProperty("versionNumber").GetInt32() }, Json));
    }

    // ── delivery zones and discount codes (through the real admin endpoints) ───────────────────────
    protected static string NewDistrict() => "District " + Guid.NewGuid().ToString("N")[..10];

    /// <summary>Creates a delivery zone in the context's branch serving one district (optionally narrowed to postal codes) with a Standard level (base 60.00,
    /// optionally free over a threshold, optionally with slot windows) and an Express level (150.00).</summary>
    protected async Task<Guid> CreateZoneAsync(
        OrderingContext context, string district, string[]? postalCodes = null, bool cod = true, decimal? codMax = null, decimal baseCharge = 60m, decimal? freeOver = null,
        object[]? slotWindows = null, int leadTimeDays = 1, string? name = null)
    {
        var response = await context.Admin.PostAsJsonAsync("/api/admin/delivery-zones", new
        {
            branchId = context.BranchId, name = name ?? "Zone " + Guid.NewGuid().ToString("N")[..10], priority = 1, isActive = true, codEnabled = cod, codMaxOrderValue = codMax,
            coverage = new[] { new { district, postalCodes = postalCodes ?? [] } },
            services = new object[]
            {
                new
                {
                    serviceLevel = "Standard", isActive = true, baseCharge, freeOverOrderValue = freeOver, leadTimeDays,
                    weightBands = Array.Empty<object>(), slotWindows = slotWindows ?? [],
                },
                new { serviceLevel = "Express", isActive = true, baseCharge = 150m, freeOverOrderValue = (decimal?)null, leadTimeDays = 0, weightBands = Array.Empty<object>(), slotWindows = Array.Empty<object>() },
            },
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("zoneId").GetGuid();
    }

    /// <summary>Opens the context's branch every day (so a delivery slot can be offered).</summary>
    protected async Task OpenBranchEveryDayAsync(OrderingContext context)
    {
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync($"/api/admin/branches/{context.BranchId}/hours",
            new { intervals = days.Select(d => new { dayOfWeek = d, opens = "00:00", closes = "23:45" }).ToArray() }, Json));
    }

    protected static string NewDiscountCode() => ("T-" + Guid.NewGuid().ToString("N"))[..14].ToUpperInvariant();

    protected async Task<(Guid Id, string Code)> CreateDiscountAsync(
        OrderingContext context, string kind = "Percentage", decimal? percent = 10m, decimal? amount = null, string scope = "Order", int? usageLimit = null, int? perCustomerLimit = null,
        bool firstOrderOnly = false, decimal? minimumSpend = null, DateTimeOffset? validTo = null, Guid[]? productIds = null, int[]? categoryIds = null)
    {
        var code = NewDiscountCode();
        var response = await context.Admin.PostAsJsonAsync("/api/admin/discount-codes", new
        {
            code, kind, percent = kind == "Percentage" ? percent : null, amount, minimumSpend, scope, productIds = productIds ?? [], categoryIds = categoryIds ?? [], firstOrderOnly,
            usageLimit, perCustomerLimit, validFrom = (DateTimeOffset?)null, validTo, isActive = true,
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return ((await ReadAsync(response)).GetProperty("discountCodeId").GetGuid(), code);
    }

    // ── the storefront branch (cart availability is advisory and read there) ───────────────────────
    private HttpClient? _storefrontStaff;

    /// <summary>Receives <paramref name="quantity"/> of a SKU into the seeded main branch's shop floor -- the storefront branch unless configured
    /// otherwise (<c>Shop:StorefrontBranchId</c>), where the cart reads availability. The SKU is the test's own, so no other test sees this stock.</summary>
    protected async Task<Guid> StockAtStorefrontAsync(Guid skuId, decimal quantity = 50m, DateOnly? expires = null)
    {
        if (_storefrontStaff is null)
        {
            var (staffId, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var assignments = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffBranchAssignmentAggregate>>();
                var assignment = new StaffBranchAssignmentAggregate(staffId);
                assignment.Assign(new StaffBranchAssignDto([AdministrationSeed.MainBranchId], AdministrationSeed.MainBranchId, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
                await assignments.SaveAsync(assignment, EventPublishOption.InMemoryPublishOnly);
            }

            _storefrontStaff = await SignInStaffAsync(mobile, password, AdministrationSeed.MainBranchId);
        }

        return await ReceiveAsync(_storefrontStaff, skuId, InventorySeed.ShopLocationId, quantity, expires);
    }

    // ── direct database access (for assertions the API does not expose) ─────────────────────────────
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

    protected static string ConnectionString { get; } = OrderingApiFactory.RequireOrderingDatabase();

    /// <summary>Registers a cat for <paramref name="ownerId"/> through the aggregate (the same way the Cat slice does).</summary>
    protected async Task<Guid> SeedCatAsync(Guid ownerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Cat " + NewCode(), "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(DateTime.UtcNow),
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>
    /// Issues a REAL HO-PHM-001 diet authorisation (the <c>DietAuthorisationAggregate</c> through its repository -- the table the real
    /// <c>IDietAuthorisationGate</c> reads) for <paramref name="catId"/> and <paramref name="productId"/>, valid from yesterday for ten days (wide enough for any
    /// branch-local date). The visit is a stand-in id: Ordering tests are not about the clinic, and Pharmacy's own tests cover the visit rules.
    /// </summary>
    protected async Task<Guid> AuthoriseDietAsync(Guid catId, Guid customerId, Guid productId, string productName)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<DietAuthorisationAggregate>>();

        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var vet = new UserContext(Guid.NewGuid(), Guid.NewGuid());
        var authorisation = new DietAuthorisationAggregate(Guid.NewGuid());
        authorisation.Authorise(new DietAuthorisationDto(
            catId, customerId, productId, productName, Guid.NewGuid(), vet.UserId, today.AddDays(-1), today.AddDays(10), null,
            today.AddDays(-1), 30, Now, new PharmacyActor(vet, Guid.NewGuid())));
        await repository.SaveAsync(authorisation, EventPublishOption.InMemoryPublishOnly);
        return authorisation.Id;
    }

    /// <summary>Moves an authorisation's whole validity window into the past, as time passing would (the gate reads the dates, not a stored status).</summary>
    protected static async Task LapseDietAuthorisationAsync(Guid authorisationId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("UPDATE pharmacy.diet_authorisation SET valid_from = valid_from - 40, valid_to = valid_to - 40 WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", authorisationId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    protected static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
