using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Infrastructure.Common.Security;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-PHM-001 §16.1 -- every test creates its own branch, staff (vet, nurse, receptionist, admin -- with passwords, for witness verification), customer, cat
/// and weight through the real endpoints (rule 1); formulary codes are minted per test (rule 2); visits, SKUs, batches and dispensary stock come from
/// HO-EMR-001, HO-SHP-001 and HO-IMP-001's own endpoints, never a direct insert (rule 3); and register entries are never cleaned up (rule 4) -- assertions
/// filter by the test's own branch.
/// </summary>
public abstract class PharmacyIntegrationTestBase(PharmacyApiFactory factory) : ConsultationIntegrationTestBase(factory), IClassFixture<PharmacyApiFactory>
{
    protected new PharmacyApiFactory Factory => factory;

    /// <summary>A formulary code minted per test -- a unique key, so never reused (§16.1 rule 2). At most 30 characters, capitals, digits, hyphen.</summary>
    protected static string NewMedicineCode() => "T-" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();

    protected static string NewLotNumber() => "LOT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    /// <summary>"Today" as production computes it (<c>PharmacyClock</c>): the main branch's local date through <c>BranchCalendar</c>, in the seeded main branch's zone -- not the UTC date,
    /// which differs from it for 18:00-24:00 UTC (backlog PHM row 29).</summary>
    /// <summary>The UTC date -- only for the cat-record endpoints (weight, allergy, deceased), whose validators reject a date after the UTC date; the branch-local date can be a day ahead of it.</summary>
    protected static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    protected static DateOnly BranchToday => BranchCalendar.LocalDateOf(DateTimeOffset.UtcNow, "Asia/Dhaka");

    // ── a world with stock ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The clinical world plus the branch's open visit and three stock locations (dispensary, warehouse, quarantine).</summary>
    protected sealed record PharmacyWorld(ClinicalWorld Clinic, Guid VisitId, Guid DispensaryId, Guid WarehouseId, Guid QuarantineId)
    {
        public HttpClient Admin => Clinic.Admin;
        public HttpClient Vet => Clinic.Vet;
        public HttpClient Nurse => Clinic.Nurse;
        public HttpClient Reception => Clinic.Reception;
        public HttpClient Customer => Clinic.Customer;
        public Guid CatId => Clinic.CatId;
        public Guid BranchId => Clinic.BranchId;
        public Guid CustomerId => Clinic.CustomerId;
        public WebApplicationFactory<Program> Api => Clinic.Api;
    }

    protected async Task<PharmacyWorld> SeedPharmacyWorldAsync(
        DateTimeOffset? start = null, bool withLocations = true, bool openVisit = true, Action<IServiceCollection>? configureServices = null)
    {
        var world = await SeedClinicalWorldAsync(start, configureServices);
        var visitId = openVisit ? await OpenVisitAsync(world.Nurse, world.AppointmentId) : Guid.Empty;

        if (!withLocations) return new PharmacyWorld(world, visitId, default, default, default);

        return new PharmacyWorld(
            world, visitId,
            await CreateLocationAsync(world.Admin, "ClinicDispensary"), await CreateLocationAsync(world.Admin, "Warehouse"), await CreateLocationAsync(world.Admin, "Quarantine"));
    }

    protected static async Task<Guid> CreateLocationAsync(HttpClient admin, string kind)
    {
        var code = "L" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync("/api/staff/inventory/locations", new { code, name = $"{kind} {Guid.NewGuid():N}"[..20], kind }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("locationId").GetGuid();
    }

    /// <summary>A second visit for the same cat (a further checked-in walk-in, then opened).</summary>
    protected async Task<Guid> OpenAnotherVisitAsync(PharmacyWorld world)
    {
        var appointmentId = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.Clinic.ServiceId);
        return await OpenVisitAsync(world.Nurse, appointmentId);
    }

    // ── SKUs and stock (through HO-SHP-001's and HO-IMP-001's real endpoints) ──────────────────────────────────────────────────

    protected sealed record MedicineSku(Guid ProductId, Guid SkuId, string Name);

    /// <summary>A clinic-only, expiry-tracked product with one priced SKU -- stocked and priced, never on the storefront (OQ-5).</summary>
    protected async Task<MedicineSku> CreateMedicineSkuAsync(
        HttpClient admin, decimal? price = 120m, bool prescriptionDiet = false, bool clinicOnly = true, string? name = null, DateTimeOffset? priceEffectiveFrom = null, bool publish = false)
    {
        var taxCode = "TAX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = taxCode, name = "Standard " + taxCode }, Json);
        Assert.True(taxClass.StatusCode == HttpStatusCode.Created, await taxClass.Content.ReadAsStringAsync());
        var taxClassId = (await taxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();

        var productName = name ?? "Test medicine " + Guid.NewGuid().ToString("N")[..8];
        var category = prescriptionDiet ? "CatFood" : "HealthAndGrooming";
        var create = await admin.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = productName, brand = "Brand-" + Guid.NewGuid().ToString("N")[..6], category, taxClassId, slug = "p-" + Guid.NewGuid().ToString("N") }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        async Task<int> VersionAsync() => (await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json)).GetProperty("version").GetInt32();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var details = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = detail.GetProperty("version").GetInt32(), name = productName, brand = detail.GetProperty("brand").GetString(), category, taxClassId,
            countryOfOriginCode = prescriptionDiet ? "DE" : (string?)null, isExpiryTracked = true, listedOnStorefront = !clinicOnly
        }, Json);
        Assert.True(details.StatusCode == HttpStatusCode.NoContent, await details.Content.ReadAsStringAsync());

        if (prescriptionDiet)
        {
            var food = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new { expectedVersion = await VersionAsync(), lifeStage = "Adult" }, Json);
            Assert.True(food.StatusCode == HttpStatusCode.NoContent, await food.Content.ReadAsStringAsync());
            var rx = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription", new { expectedVersion = await VersionAsync(), prescriptionRequired = true }, Json);
            Assert.True(rx.StatusCode == HttpStatusCode.NoContent, await rx.Content.ReadAsStringAsync());
        }

        var addSku = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus", new { expectedVersion = await VersionAsync(), code = "SKU" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant() }, Json);
        Assert.True(addSku.StatusCode == HttpStatusCode.Created, await addSku.Content.ReadAsStringAsync());
        var skuId = (await addSku.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        if (price is { } amount)
        {
            var priced = await admin.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices", new { amount, currencyCode = "BDT", effectiveFrom = priceEffectiveFrom ?? DateTimeOffset.UtcNow }, Json);
            Assert.True(priced.StatusCode == HttpStatusCode.Created, await priced.Content.ReadAsStringAsync());
        }

        if (publish)
        {
            var published = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish", new { expectedVersion = await VersionAsync() }, Json);
            Assert.True(published.StatusCode == HttpStatusCode.NoContent, await published.Content.ReadAsStringAsync());
        }

        return new MedicineSku(productId, skuId, productName);
    }

    /// <summary>Receives <paramref name="quantity"/> into a new batch at <paramref name="locationId"/> (default: the dispensary) and returns the batch id.</summary>
    protected static async Task<Guid> ReceiveAsync(HttpClient admin, Guid skuId, Guid locationId, decimal quantity, DateOnly? expires = null, string? lot = null)
    {
        var line = new
        {
            skuId, locationId, lotNumber = lot ?? NewLotNumber(), expiresOn = expires ?? BranchToday.AddDays(300), quantityAccepted = quantity, quantityRejected = 0m,
            rejectionReason = (string?)null, purchaseOrderLineId = (Guid?)null, unitCostAmount = 5m, unitCostCurrency = "BDT", holdForInspection = false,
            exchangeRateToBase = (decimal?)null
        };

        var response = await admin.PostAsJsonAsync("/api/staff/inventory/goods-receipts",
            new { purchaseOrderId = (Guid?)null, consignmentId = (Guid?)null, supplierId = (Guid?)null, lines = new[] { line } }, Json);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return Assert.Single(body.GetProperty("batchIds").EnumerateArray()).GetGuid();
    }

    /// <summary>The ONE direct write any Pharmacy test makes, copied from the Inventory suite: a real receipt refuses expired goods (BR-3), so a test that needs
    /// an already-expired batch sets <c>expires_on</c> into the past here.</summary>
    protected static async Task SetBatchExpiryDirectlyAsync(Guid batchId, DateOnly expiresOn)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("UPDATE inventory.batch SET expires_on = @on WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("on", expiresOn);
        command.Parameters.AddWithValue("id", batchId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    protected static async Task<decimal> OnHandAsync(Guid batchId, Guid locationId) =>
        await ScalarAsync<decimal?>("SELECT on_hand FROM inventory.stock_position WHERE batch_id = @b AND location_id = @l", ("b", batchId), ("l", locationId)) ?? 0m;

    // ── the formulary ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Meloxicam-like by default: 1.5 mg per ml, per-kg dosing 0.05-0.1 mg/kg, oral, one linked SKU.</summary>
    protected static object MedicineBody(
        string? code = null, string? name = null, string[]? ingredients = null, string[]? classes = null, Guid[]? skuIds = null, string dosingBasis = "PerKg",
        object? doseRange = null, object[]? interactions = null, string form = "OralLiquid", object? strength = null, string dispensingUnit = "Millilitre",
        string[]? routes = null, string contraindications = "Do not use in dehydrated cats.", int expectedVersion = 0) => new
    {
        code = code ?? NewMedicineCode(),
        name = name ?? "Test meloxicam " + Guid.NewGuid().ToString("N")[..6],
        activeIngredients = ingredients ?? ["meloxicam"],
        drugClasses = classes ?? ["nsaid"],
        form,
        strength = strength ?? new { amount = 1.5m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" },
        defaultRoute = (routes ?? ["Oral"])[0],
        allowedRoutes = routes ?? ["Oral"],
        dosingBasis,
        doseRange = dosingBasis == "PerKg" ? doseRange ?? new { minMgPerKg = 0.05m, maxMgPerKg = 0.1m } : null,
        contraindications,
        interactions = interactions ?? [],
        dispensingUnit,
        linkedSkuIds = skuIds ?? [],
        expectedVersion
    };

    protected static async Task<Guid> CreateMedicineAsync(HttpClient admin, object? body = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/formulary", body ?? MedicineBody(), Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("medicineId").GetGuid();
    }

    protected static async Task SetControlledAsync(HttpClient admin, Guid medicineId, bool controlled = true, string schedule = "CD Schedule 2", string? reason = null)
    {
        var response = await admin.PutAsJsonAsync($"/api/admin/formulary/{medicineId}/controlled", new { isControlled = controlled, controlledSchedule = controlled ? schedule : null, reason }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    // ── the cat's facts (HO-CAT-001's real endpoints) ───────────────────────────────────────────────────────────────────────────

    protected static async Task RecordWeightAsync(HttpClient staff, Guid catId, decimal kilograms, DateOnly? recordedOn = null)
    {
        var response = await staff.PostAsJsonAsync($"/api/cats/{catId}/weights", new { kilograms, recordedOn = recordedOn ?? UtcToday }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    protected static async Task SetAllergiesAsync(HttpClient staff, Guid catId, params string[] allergens)
    {
        var response = await staff.PutAsJsonAsync($"/api/cats/{catId}/medical-profile", new
        {
            allergies = allergens.Select(a => new { allergen = a, reaction = "Hives", severity = "Moderate", recordedOn = UtcToday }).ToArray(),
            chronicConditions = Array.Empty<object>(), medications = Array.Empty<object>()
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    // ── visits and cats ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Records vitals, writes the note with a seeded system diagnosis and finalises the visit as the vet -- the shortest legitimate way to a finalised visit.</summary>
    protected static async Task FinaliseVisitAsync(ClinicalWorld world, Guid visitId)
    {
        var vitals = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m }, Json);
        Assert.Equal(HttpStatusCode.Created, vitals.StatusCode);

        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Routine", plan = "None",
            problems = new[] { new { diagnosisCodeId = DiagnosisCodeSeed.RoutineNoAbnormality.Id, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", new { expectedVersion = (await ReadAsync(save)).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalised.StatusCode);
    }

    protected static async Task MarkCatDeceasedAsync(HttpClient admin, Guid catId)
    {
        var response = await admin.PostAsJsonAsync($"/api/cats/{catId}/deceased", new { deceasedOn = UtcToday }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    protected sealed record AuditRow(string Action, AuditCategory Category, Guid? CorrelationId, Guid ActorUserId, string? BeforeValue, string? AfterValue, string Reason);

    /// <summary>Every audit row written about one entity (its aggregate type name and id).</summary>
    protected async Task<List<AuditRow>> AuditRowsAsync(string entityType, Guid entityId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        return await db.Set<CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .Select(a => new AuditRow(a.Action, a.Category, a.CorrelationId, a.ActorUserId, a.BeforeValue, a.AfterValue, a.Reason))
            .ToListAsync();
    }

    // ── the register through its published port (the way HO-SRG-001, HO-EMR-001 and HO-IMP-001 write to it) ───────────────────────

    /// <summary>A second host (same database, same fake clock) whose caller is a fixed staff member -- a consumer that enlists register entries inside its own save has no HTTP request
    /// to say who is acting, exactly like the Inventory suite's port host.</summary>
    protected static WebApplicationFactory<Program> PortHostFor(PharmacyWorld world, Guid userId) =>
        world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserContextProvider>();
            services.AddScoped<IUserContextProvider>(_ => new FixedUserContextProvider(userId));
        }));

    private sealed class FixedUserContextProvider(Guid userId) : IUserContextProvider
    {
        public UserContext GetUserContext() => new(userId, Guid.Parse("00000000-0000-0000-0000-000000000002")) { UserName = "port-test", Roles = ["Vet"] };
    }

    protected static ControlledDrugRegisterEntryDto RegisterDto(
        Guid branchId, Guid medicineId, ControlledDrugEntryKind kind, decimal delta, Guid actorId, Guid? witnessId = null, Guid? skuId = null, Guid? batchId = null,
        string lot = "LOT-1", Guid? catId = null, Guid? customerId = null, string name = "Test controlled", string? notes = null, string? doseText = null,
        ControlledDrugSourceType? source = null, Guid? sourceId = null) => new(
        branchId, medicineId, name, "10 mg/ml", MedicineForm.Injection, "CD Schedule 2", DispensingUnit.Millilitre, skuId, batchId, lot, BranchToday.AddDays(100), kind, delta,
        doseText, null, catId, customerId, actorId, witnessId, source ?? ControlledDrugSourceType.GoodsReceipt, sourceId ?? Guid.NewGuid(), null, null, null, DateTimeOffset.UtcNow, notes);

    /// <summary>Enlists the entries through <see cref="IControlledDrugRegister"/> and commits them in one save (the consumer's own save). Returns the new entry ids.</summary>
    protected static async Task<IReadOnlyList<Guid>> EnlistAsync(WebApplicationFactory<Program> host, params ControlledDrugRegisterEntryDto[] entries)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var register = scope.ServiceProvider.GetRequiredService<IControlledDrugRegister>();
        var ids = entries.Select(e => register.Enlist(e)).ToList();
        await scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>().SaveChangesAsync();
        return ids;
    }

    /// <summary>Runs one full worker pass (the host's clock is a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>: advance it first to pass the settle delay).</summary>
    protected static async Task RunPharmacyCycleAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.PharmacyWorkCycle>().RunAsync();
    }

    // ── answers ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    protected static async Task<string> BodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    /// <summary>The violation codes in a 400/409 body (the validation shape's <c>errors[].errorCode</c>).</summary>
    protected static async Task<List<string>> ErrorCodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        if (!body.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array) return [];
        return errors.EnumerateArray().Select(e => e.GetProperty("errorCode").GetString()!).ToList();
    }

    protected static async Task AssertErrorAsync(HttpResponseMessage response, string expectedCode, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"expected {(int)status} {expectedCode} but got {(int)response.StatusCode}: {text}");
        Assert.Contains(expectedCode, text);
    }
}
