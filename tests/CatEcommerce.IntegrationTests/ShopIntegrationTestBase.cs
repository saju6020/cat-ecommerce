using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-SHP-001 §16 -- §16.1 rules apply: every catalogue assertion is scoped to the test's
/// own data (a fresh, unique brand/slug/SKU code/barcode per test), never a global count.</summary>
public abstract class ShopIntegrationTestBase(ShopApiFactory factory) : CatEcommerceIntegrationTestBase<ShopApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string UniqueSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // Short (8 hex chars) so it fits every narrow code column this helper feeds -- notably
    // admin.tax_class.code, which is varchar(30).
    protected static string UniqueCode(string prefix) => $"{prefix.ToUpperInvariant()}-{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();

    protected sealed record SeededProduct(Guid ProductId, string Slug, Guid SkuId, string SkuCode);

    /// <summary>The staff detail's current <c>version</c> -- every staff write carries it.</summary>
    protected async Task<int> ProductVersionAsync(HttpClient staff, Guid productId)
    {
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        return detail.GetProperty("version").GetInt32();
    }

    /// <summary>Creates a product through the real staff endpoints, prices its first SKU now and
    /// (by default) publishes it. Food products get a country of origin and life stage so they can
    /// publish. §16.1 rule 1/2: every identifier is minted per call.</summary>
    protected async Task<SeededProduct> CreateProductAsync(
        HttpClient staff, string brand, string name, string category = "HealthAndGrooming", decimal price = 1000m,
        string? countryCode = null, string? lifeStage = null, bool prescription = false, string? ingredientList = null,
        string[]? healthFocus = null, string? proteinSource = null, bool publish = true, string? tag = null, string? subcategory = null)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("p");
        var isFood = category == "CatFood";

        var create = await staff.PostAsJsonAsync("/api/staff/shop/products", new { kind = "Standard", name, brand, category, subcategory, taxClassId, slug }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"{(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        var details = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId), name, brand, category, subcategory, taxClassId,
            countryOfOriginCode = isFood ? (countryCode ?? "DE") : countryCode,
            tags = tag is null ? Array.Empty<string>() : [tag],
            healthFocus = healthFocus ?? []
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, details.StatusCode);

        if (isFood)
        {
            var food = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new
            { expectedVersion = await ProductVersionAsync(staff, productId), lifeStage = lifeStage ?? "Adult", ingredientList, proteinSource }, Json);
            Assert.Equal(HttpStatusCode.NoContent, food.StatusCode);
        }

        if (prescription)
        {
            var rx = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription",
                new { expectedVersion = await ProductVersionAsync(staff, productId), prescriptionRequired = true }, Json);
            Assert.Equal(HttpStatusCode.NoContent, rx.StatusCode);
        }

        var skuCode = UniqueCode("SKU");
        var addSku = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, productId), code = skuCode }, Json);
        Assert.Equal(HttpStatusCode.Created, addSku.StatusCode);
        var skuId = (await addSku.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        await SchedulePriceAsync(staff, skuId, price);

        if (publish) await PublishAsync(staff, productId);

        return new SeededProduct(productId, slug, skuId, skuCode);
    }

    protected async Task SchedulePriceAsync(HttpClient staff, Guid skuId, decimal price, DateTimeOffset? effectiveFrom = null)
    {
        var response = await staff.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices",
            new { amount = price, currencyCode = "BDT", effectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    protected async Task PublishAsync(HttpClient staff, Guid productId)
    {
        var publish = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
            new { expectedVersion = await ProductVersionAsync(staff, productId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);
    }

    /// <summary>Creates a bundle product (one SKU of its own) with the given components and prices
    /// its SKU; publishes by default. Components are (component SKU id, quantity).</summary>
    protected async Task<SeededProduct> CreateBundleAsync(
        HttpClient staff, string brand, string name, IReadOnlyList<(Guid SkuId, int Quantity)> components, decimal price = 2500m, bool publish = true)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("bundle");
        var skuCode = UniqueCode("BND");

        var create = await staff.PostAsJsonAsync("/api/staff/shop/products", new
        { kind = "Bundle", name, brand, category = "HealthAndGrooming", taxClassId, slug, bundleSkuCode = skuCode }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"{(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var skuId = detail.GetProperty("skus")[0].GetProperty("skuId").GetGuid();

        var set = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/bundle-components", new
        {
            expectedVersion = detail.GetProperty("version").GetInt32(),
            components = components.Select(c => new { componentSkuId = c.SkuId, quantity = c.Quantity }).ToArray()
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        await SchedulePriceAsync(staff, skuId, price);
        if (publish) await PublishAsync(staff, productId);

        return new SeededProduct(productId, slug, skuId, skuCode);
    }

    /// <summary>
    /// Test-only availability fixture (HO-SHP-001 §1 Amended, 2026-10-04). HO-IMP-001's
    /// <c>inventory.v_sellable_stock</c> / <c>IStockLedger</c> do not exist in this worktree, so
    /// the only availability state Shop reads today is "an Active SKU with a price effective now"
    /// (§1 Amended, 2026-09-28). This moves every price of the SKU into the future (= nothing
    /// sellable) and <see cref="SetSkuInStockAsync"/> reverses it -- the stand-in for "Inventory
    /// holds no / holds stock". No production code path and no Testing-only endpoint sets stock
    /// (§16.1 rule 3); when HO-IMP-001 lands these two helpers are replaced by ledger fixtures.
    /// </summary>
    protected async Task SetSkuOutOfStockAsync(Guid skuId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE product.sku_price SET effective_from = now() + interval '1 day' + (random() * interval '1 hour') WHERE sku_id = {skuId}");
    }

    protected async Task SetSkuInStockAsync(Guid skuId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE product.sku_price SET effective_from = now() - interval '1 minute' - (random() * interval '1 minute') WHERE sku_id = {skuId}");
    }

    protected async Task<Guid> StaffSkuIdAsync(HttpClient staff, Guid productId)
    {
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        return detail.GetProperty("skus")[0].GetProperty("skuId").GetGuid();
    }

    /// <summary>Seeds an alive cat owned by <paramref name="ownerId"/> directly through the
    /// repository (HO-CAT-001's own aggregate, §16.1 rule 4 style).</summary>
    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Mitu", DateOnly? dateOfBirth = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatAggregate>>();

        var cat = new CatEcommerce.Domain.Aggregates.Cat.CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(DateTime.UtcNow),
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>Records the medical facts the food recommendation reads (allergies, chronic
    /// conditions), the neuter status and a weight -- all through HO-CAT-001's own aggregates, the
    /// same way <see cref="SeedCatAsync"/> seeds the cat. Any argument left null is not recorded.</summary>
    protected async Task RecordCatFactsAsync(
        Guid catId, string[]? allergens = null, string[]? conditions = null, bool? neutered = null, decimal? weightKg = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatAggregate>>();
        var weights = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatWeightRecordAggregate>>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userContext = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        var cat = await repository.GetByIdAsync(catId);

        if (allergens is not null || conditions is not null)
        {
            cat!.UpdateMedicalProfile(new CatMedicalProfileDto(
                (allergens ?? []).Select(a => new CatEcommerce.Shared.SharedDto.Cat.AllergyInput(a, "Itching", AllergySeverity.Mild, today)).ToList(),
                (conditions ?? []).Select(c => new CatEcommerce.Shared.SharedDto.Cat.ChronicConditionInput(c, null, null)).ToList(),
                [], null, null, userContext, Guid.NewGuid()));
            await repository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
            cat = await repository.GetByIdAsync(catId);
        }

        if (neutered is true)
        {
            cat!.RecordNeuterStatus(new CatNeuterStatusDto(NeuterStatus.Neutered, today.AddMonths(-6), today, userContext, Guid.NewGuid()));
            await repository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
        }

        if (weightKg is { } kilograms)
        {
            var record = new CatEcommerce.Domain.Aggregates.Cat.CatWeightRecordAggregate(Guid.NewGuid());
            record.Record(new CatWeightRecordDto(catId, kilograms, today, Guid.NewGuid(), null, today, userContext, Guid.NewGuid()));
            await weights.SaveAsync(record, EventPublishOption.InMemoryPublishOnly);
        }
    }

    protected async Task MarkCatDeceasedAsync(Guid catId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatAggregate>>();

        var cat = await repository.GetByIdAsync(catId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        cat!.MarkDeceased(new CatDeceasedDto(today, null, today, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        var rawValue = header[(cookieName.Length + 1)..].Split(';')[0];
        return Uri.UnescapeDataString(rawValue);
    }

    /// <summary>Seeds a staff account with the given roles directly through the repository (§16.1),
    /// gives it a password, then returns credentials for a real sign-in.</summary>
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

    protected async Task<HttpClient> SignInStaffAsync(string mobile, string password, WebApplicationFactory<Program>? factory = null)
    {
        var client = factory is null ? CreateClient() : factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    /// <summary>A shop-manager client, optionally on a specific factory (e.g. the one with a second
    /// content locale enabled -- the translation endpoints depend on the host's configuration).</summary>
    protected async Task<HttpClient> SignInAsShopManagerAsync(WebApplicationFactory<Program>? factory = null)
    {
        var (_, mobile, password) = await SeedStaffAsync("ShopManager");
        return await SignInStaffAsync(mobile, password, factory);
    }

    /// <summary>Seeds an active tax class directly through the repository -- HO-ADM-001's own
    /// aggregate, read here the same way HO-CAT-001's cat is seeded by other areas' bases.</summary>
    protected async Task<Guid> SeedActiveTaxClassAsync(string code)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<TaxClassAggregate>>();

        var taxClass = new TaxClassAggregate(Guid.NewGuid());
        taxClass.Create(new TaxClassCreateDto(code, "Standard " + code, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(taxClass, EventPublishOption.InMemoryPublishOnly);
        return taxClass.Id;
    }

    /// <summary>Registers and signs in a fresh customer on a fresh client (real endpoints), CSRF
    /// header attached -- the same pattern <c>AdministrationIntegrationTestBase</c> uses. Accepts an
    /// explicit factory so a §16.1-rule-4 controllable-double factory can be used too.</summary>
    protected async Task<(HttpClient Client, Guid CustomerId)> RegisterAndSignInCustomerAsync(WebApplicationFactory<Program>? factory = null, string? email = null)
    {
        var client = (factory ?? Factory).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });

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

        if (email is not null)
        {
            var update = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = "Test Customer", emailAddress = email, additionalPhoneNumbers = Array.Empty<string>(), currentPassword = password
            }, Json);
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        }

        return (client, customerId);
    }

    protected static string NewEmail() => $"t-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// A units-sold figure that grows with the wall clock. The catalogue is global and never
    /// truncated (§16.1 rule 1), and the "newest first / most popular" candidate pools of the
    /// recommendation reads would otherwise let earlier runs' products crowd a fresh test's own
    /// products out: a newer run's figure always outranks an older run's.
    /// </summary>
    protected static int NextUnitsSold() => (int)Math.Min(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), int.MaxValue - 1_000);

    /// <summary>Pushes the units-sold copy exactly as HO-ORD-001 will: the in-process
    /// <c>RecordProductSalesStatsCommand</c> (no HTTP route exists for it).</summary>
    protected async Task PushUnitsSoldAsync(Guid productId, int unitsSold)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var response = await dispatcher.SendAsync(new CatEcommerce.Application.Commands.Product.RecordProductSalesStatsCommand
        {
            ProductId = productId, UnitsSold90d = unitsSold, AsOf = DateTimeOffset.UtcNow,
            UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        Assert.True(response.IsSuccess);
    }

    /// <summary>Reads a successful response as JSON; a failure carries the status and body so a
    /// flaky 4xx/5xx is diagnosable from the test report.</summary>
    protected static async Task<JsonElement> JsonOkAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body, Json);
    }

    /// <summary>Gives two customers a shared additional phone number (through the real profile
    /// endpoint) so HO-ACC-001's merge accepts them as duplicates (<c>CustomersNotDuplicates</c>
    /// otherwise).</summary>
    protected static async Task ShareContactNumberAsync(HttpClient customer, string sharedNumber)
    {
        var update = await customer.PutAsJsonAsync("/api/customers/me", new
        { fullName = "Test Customer", additionalPhoneNumbers = new[] { sharedNumber } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }

    /// <summary>Grants (or withdraws) marketing consent on one channel through the real
    /// notification-preferences endpoint -- back-in-stock is a Marketing template (F33, OQ-2).</summary>
    protected static async Task SetMarketingConsentAsync(HttpClient customer, string channel, bool granted)
    {
        var response = await customer.PutAsJsonAsync("/api/customers/me/notification-preferences",
            new { changes = new[] { new { channel, category = "Marketing", isGranted = granted } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Runs one <c>ShopWorkCycle</c> pass in a fresh scope -- the hosted worker is off in
    /// Testing, so a test drives the same pass directly (the HO-NTF-001/HO-APT-001 pattern).</summary>
    protected static async Task RunShopCycleAsync(WebApplicationFactory<Program> apiFactory)
    {
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var cycle = scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.ShopWorkCycle>();
        await cycle.RunAsync();
    }

    /// <summary>One HO-NTF-001 delivery pass (expand pending requests, deliver due notifications).</summary>
    protected static async Task RunNotificationDeliveryPassAsync(WebApplicationFactory<Program> apiFactory)
    {
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var workCycle = scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.NotificationWorkCycle>();
        await workCycle.RunDeliveryPassAsync();
    }

    /// <summary>
    /// Bounded loop-until-own-row delivery (the AppointmentsIntegrationTestBase precedent, ACCEPTANCE
    /// 2026-09-29): a single global pass is not reliable evidence on the shared, never-truncated
    /// database -- another class's leftover Pending rows can crowd this test's own row out of one
    /// pass's batch cap. Passes run until <paramref name="isDone"/> holds (or the test fails loudly),
    /// never past that. Classes that call this carry <c>[Collection("Notifications")]</c>.
    /// </summary>
    protected static async Task RunNotificationDeliveryUntilAsync(
        WebApplicationFactory<Program> apiFactory, Func<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext, Task<bool>> isDone, int maxPasses = 200)
    {
        for (var pass = 0; pass < maxPasses; pass++)
        {
            await RunNotificationDeliveryPassAsync(apiFactory);

            await using var scope = apiFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            if (await isDone(db)) return;
        }

        Assert.Fail($"RunNotificationDeliveryUntilAsync did not reach the expected state within {maxPasses} passes.");
    }
}
