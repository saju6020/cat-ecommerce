using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-BIL-001 Phase J (owner decisions 2026-10-14) -- automatic billing from a finalised visit, a dispensing and a discharged surgery, end to end through the
/// real endpoints of EMR, Pharmacy, Surgery and Inventory (no <c>clinic.*</c>, <c>pharmacy.*</c> or <c>inventory.*</c> row is written by a test, except the
/// two documented below). Billing is read through <c>/api/staff/billing/charges</c>.
/// Two direct writes, both to rows another area owns and both only to set a scene the API cannot: a cat's owner (to prove the owner AT FINALISING is billed) and a
/// customer's <c>merged_into_customer_id</c> (the merge endpoint needs a shared contact number and also moves the cat, which would hide the point).
/// </summary>
public sealed class BillingPhaseJEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A host in which the visit-finalised capture handler is not registered: the way to make a visit "uncaptured" without breaking its data, so the
    /// recover path, replay and concurrency are exercised on a visit that has no charge yet.</summary>
    private static void WithoutVisitCaptureHandler(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(CaptureChargesOnVisitFinalisedHandler)).ToList())
            services.Remove(descriptor);
    }

    private static async Task SetDiagnosticPriceAsync(HttpClient admin, string key, string value)
    {
        var response = await admin.PutAsJsonAsync("/api/admin/settings", new { changes = new[] { new { key, value } } }, Json);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<List<JsonElement>> ChargesOfSourceAsync(HttpClient admin, Guid customerId, string sourceKind, Guid sourceId)
    {
        var page = await ReadAsync(await admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&pageSize=200"));
        return page.GetProperty("items").EnumerateArray()
            .Where(c => c.GetProperty("sourceKind").GetString() == sourceKind && c.GetProperty("sourceId").GetGuid() == sourceId)
            .OrderBy(c => c.GetProperty("sourceLineRef").GetString(), StringComparer.Ordinal).ToList();
    }

    private static async Task<JsonElement> CaptureAsync(HttpClient admin, string kind, Guid sourceId)
    {
        var response = await admin.PostAsync($"/api/staff/billing/sources/{kind}/{sourceId}/capture", null);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await ReadAsync(response);
    }

    private static decimal Quantity(JsonElement charge) => decimal.Parse(charge.GetProperty("quantity").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static decimal Amount(JsonElement charge) => decimal.Parse(charge.GetProperty("unitPrice").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<Guid> OrderDiagnosticAsync(HttpClient vet, Guid visitId, string kind, string? description = null)
    {
        var response = await vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind, description, analyteIds = Array.Empty<Guid>() }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("diagnosticOrderId").GetGuid();
    }

    private static async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    /// <summary>A branch-local (Asia/Dhaka, UTC+6, no DST) calendar date.</summary>
    private static DateOnly DhakaDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(6)).DateTime);

    private async Task<Guid> OtherCustomerAsync(PharmacyWorld world)
    {
        var response = await world.Admin.PostAsJsonAsync("/api/staff/customers", new { fullName = "Other Owner " + Guid.NewGuid().ToString("N")[..6], mobileNumber = "01" + Random.Shared.NextInt64(100_000_000, 999_999_999) }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("customerId").GetGuid();
    }

    /// <summary>A world with an open visit, a dispensary-stocked priced consumable, and one procedure/consumable/two-diagnostics visit ready to finalise.</summary>
    private sealed record Visit(PharmacyWorld World, Guid VisitId, Guid SkuId, Guid CancelledOrderId);

    private async Task<Visit> PrepareVisitAsync(Action<IServiceCollection>? configureServices = null, DateTimeOffset? start = null)
    {
        var world = await SeedPharmacyWorldAsync(start, configureServices: configureServices);
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceBloodWork, "350.00");
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceImaging, "0.00");

        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m);

        var consumable = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments", new { kind = "Consumable", skuId = sku.SkuId, quantity = 2m, isBillable = true }, Json);
        Assert.True(consumable.StatusCode == HttpStatusCode.Created, await consumable.Content.ReadAsStringAsync());
        var procedure = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments",
            new { kind = "Procedure", description = "Wound dressing", serviceId = world.Clinic.ServiceId, quantity = 1m, isBillable = true }, Json);
        Assert.True(procedure.StatusCode == HttpStatusCode.Created, await procedure.Content.ReadAsStringAsync());
        // A given-but-not-billable item and a reversed line are never billed.
        var free = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments", new { kind = "Procedure", description = "Nail trim (no charge)", quantity = 1m, isBillable = false }, Json);
        Assert.True(free.StatusCode == HttpStatusCode.Created, await free.Content.ReadAsStringAsync());

        await OrderDiagnosticAsync(world.Vet, world.VisitId, "BloodWork");
        await OrderDiagnosticAsync(world.Vet, world.VisitId, "Imaging", "Abdominal ultrasound");
        var cancelled = await OrderDiagnosticAsync(world.Vet, world.VisitId, "Urinalysis");
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{cancelled}/cancel", new { reason = "ordered by mistake" }, Json)).StatusCode);

        return new Visit(world, world.VisitId, sku.SkuId, cancelled);
    }

    // ── J-1 the visit is its own bill ────────────────────────────────────────────────────────────

    [Fact]
    public async Task J1_AFinalisedVisit_BecomesItsOwnBill_OneLinePerBillableTreatmentAndOnePerDiagnosticTest_NotTheConsultationFee()
    {
        var visit = await PrepareVisitAsync();
        var world = visit.World;

        await FinaliseVisitAsync(world.Clinic, visit.VisitId);

        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId);
        Assert.Equal(4, charges.Count); // procedure + consumable + blood work + imaging; not the free procedure, not the cancelled urinalysis
        Assert.All(charges, c =>
        {
            Assert.Equal("Clinic", c.GetProperty("businessLine").GetString());
            Assert.Equal(world.CatId, c.GetProperty("catId").GetGuid());
            Assert.Equal(DhakaDate(world.Clinic.Clock.GetUtcNow()).ToString("yyyy-MM-dd"), c.GetProperty("serviceDate").GetString()); // branch-local, Asia/Dhaka
            Assert.Equal("BDT", c.GetProperty("unitPrice").GetProperty("currencyCode").GetString());
        });

        var diagnostics = charges.Where(c => c.GetProperty("sourceLineRef").GetString()!.StartsWith("diagnostic:", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("Service", d.GetProperty("itemKind").GetString()));
        Assert.Equal([0m, 350m], diagnostics.Select(Amount).Order().ToArray()); // the admin's price, and the default zero priced as a zero line
        Assert.Contains(diagnostics, d => d.GetProperty("description").GetString() == "Blood work");
        Assert.Contains(diagnostics, d => d.GetProperty("description").GetString() == "Imaging: Abdominal ultrasound");

        var consumable = Assert.Single(charges, c => c.GetProperty("itemKind").GetString() == "Product");
        Assert.Equal(120m, Amount(consumable));
        Assert.Equal(2m, Quantity(consumable));

        var procedure = Assert.Single(charges, c => c.GetProperty("sourceLineRef").GetString()!.StartsWith("treatment:", StringComparison.Ordinal) && c.GetProperty("itemKind").GetString() == "Service");
        Assert.Equal("Wound dressing", procedure.GetProperty("description").GetString());

        // The diagnostic tests are billed under the visit's service's tax class, the procedure under the service's own: the same class here.
        Assert.All(diagnostics, d => Assert.Equal(procedure.GetProperty("taxClassId").GetGuid(), d.GetProperty("taxClassId").GetGuid()));

        // The consultation fee is the appointment's, charged when the appointment completes -- never by the visit.
        var all = (await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/charges?customerId={world.CustomerId}&pageSize=200"))).GetProperty("items").EnumerateArray();
        Assert.DoesNotContain(all, c => c.GetProperty("sourceKind").GetString() == "Appointment");

        // A visit with something to bill and a charge is not "uncaptured".
        var today = DhakaDate(world.Clinic.Clock.GetUtcNow());
        var uncaptured = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        Assert.DoesNotContain(uncaptured.EnumerateArray(), e => e.GetProperty("sourceId").GetGuid() == visit.VisitId);
    }

    [Fact]
    public async Task J1b_AVisitWithNothingToBill_HasNoCharges_AndIsNotListedAsUncaptured()
    {
        var world = await SeedPharmacyWorldAsync();

        await FinaliseVisitAsync(world.Clinic, world.VisitId);

        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", world.VisitId));
        var today = DhakaDate(world.Clinic.Clock.GetUtcNow());
        var uncaptured = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        Assert.DoesNotContain(uncaptured.EnumerateArray(), e => e.GetProperty("sourceId").GetGuid() == world.VisitId);
    }

    // ── J-2 who is billed ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task J2_TheCatsOwnerAtFinalising_IsBilled_StoredOnTheVisit_AndNeverChangesLater()
    {
        var visit = await PrepareVisitAsync();
        var world = visit.World;
        var newOwner = await OtherCustomerAsync(world);
        await ExecuteAsync("UPDATE clinic.cat SET \"OwnerId\" = @o WHERE \"Id\" = @c", ("o", newOwner), ("c", world.CatId)); // the cat changed hands after the appointment was booked

        await FinaliseVisitAsync(world.Clinic, visit.VisitId);

        Assert.Equal(newOwner, await ScalarAsync<Guid>("SELECT customer_id FROM clinic.visit WHERE \"Id\" = @v", ("v", visit.VisitId)));
        Assert.Equal(4, (await ChargesOfSourceAsync(world.Admin, newOwner, "Visit", visit.VisitId)).Count);
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId)); // not the appointment's customer

        // The cat changes hands again: the finalised visit's billing customer does not follow (the frozen row cannot change).
        await ExecuteAsync("UPDATE clinic.cat SET \"OwnerId\" = @o WHERE \"Id\" = @c", ("o", world.CustomerId), ("c", world.CatId));
        Assert.Equal(newOwner, await ScalarAsync<Guid>("SELECT customer_id FROM clinic.visit WHERE \"Id\" = @v", ("v", visit.VisitId)));
        var again = await CaptureAsync(world.Admin, "Visit", visit.VisitId);
        Assert.Equal(0, again.GetProperty("captured").GetInt32());
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId));
    }

    [Fact]
    public async Task J2b_AnOwnerMergedAwayBeforeCapture_IsBilledAsTheSurvivor()
    {
        var visit = await PrepareVisitAsync(WithoutVisitCaptureHandler);
        var world = visit.World;
        await FinaliseVisitAsync(world.Clinic, visit.VisitId);
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId)); // nothing captured yet

        var survivor = await OtherCustomerAsync(world);
        await ExecuteAsync("UPDATE account.customer_account SET merged_into_customer_id = @s WHERE \"Id\" = @d", ("s", survivor), ("d", world.CustomerId));

        var result = await CaptureAsync(world.Admin, "Visit", visit.VisitId);

        Assert.Equal(4, result.GetProperty("captured").GetInt32());
        Assert.Equal(4, (await ChargesOfSourceAsync(world.Admin, survivor, "Visit", visit.VisitId)).Count);
        Assert.Equal(world.CustomerId, await ScalarAsync<Guid>("SELECT customer_id FROM clinic.visit WHERE \"Id\" = @v", ("v", visit.VisitId))); // the record itself is untouched
    }

    // ── J-3 once per source ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task J3_ReplayingTheEvent_AndRecapturing_RecordsNothingTwice()
    {
        var visit = await PrepareVisitAsync();
        var world = visit.World;
        await FinaliseVisitAsync(world.Clinic, visit.VisitId);
        Assert.Equal(4, (await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId)).Count);

        // Replay the very event, through the host's own handler registration, three times.
        await using (var scope = world.Api.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetServices<IEventHandlerAsync<VisitFinalisedEvent>>().OfType<CaptureChargesOnVisitFinalisedHandler>().Single();
            for (var i = 0; i < 3; i++)
                await handler.HandleAsync(new VisitFinalisedEvent
                {
                    VisitId = visit.VisitId, AppointmentId = Guid.NewGuid(), CatId = world.CatId, BranchId = world.BranchId, CustomerId = world.CustomerId,
                    FinalisedBy = world.Clinic.VetStaffId, FinalisedOn = world.Clinic.Clock.GetUtcNow(),
                    UserContext = new Platform.Infrastructure.Common.Security.UserContext(world.Clinic.VetStaffId, Guid.NewGuid()), CorrelationId = Guid.NewGuid(),
                });
        }

        Assert.Equal(0, (await CaptureAsync(world.Admin, "Visit", visit.VisitId)).GetProperty("captured").GetInt32());
        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId);
        Assert.Equal(4, charges.Count);
        Assert.Equal(4, charges.Select(c => c.GetProperty("sourceLineRef").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task J3b_TwoConcurrentCaptures_OfTheSameVisit_LeaveEachLineExactlyOnce()
    {
        var visit = await PrepareVisitAsync(WithoutVisitCaptureHandler);
        var world = visit.World;
        await FinaliseVisitAsync(world.Clinic, visit.VisitId);
        // A barrier: all three requests are built and waiting, then released together.
        var gate = new TaskCompletionSource();
        var calls = Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visit.VisitId}/capture", null);
        })).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(calls);

        // Whatever each caller was told (a loser may be answered with the unique-index conflict), the book is right: each line once.
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"{(int)r.StatusCode}"));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        // A loser stops early; a repeat capture completes the set (the partly captured visit is still on the uncaptured list until then).
        await CaptureAsync(world.Admin, "Visit", visit.VisitId);
        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId);
        Assert.Equal(4, charges.Count);
        Assert.Equal(4, charges.Select(c => c.GetProperty("sourceLineRef").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task J3c_ConcurrentFinalisation_BillsOnce()
    {
        var visit = await PrepareVisitAsync();
        var world = visit.World;
        await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visit.VisitId}/vitals", new { temperatureC = 38.5m }, Json);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visit.VisitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visit.VisitId}/note", new
        {
            expectedVersion = version, assessment = "Routine", plan = "None",
            problems = new[] { new { diagnosisCodeId = CatEcommerce.Shared.Common.Constants.DiagnosisCodeSeed.RoutineNoAbnormality.Id, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var expected = (await ReadAsync(save)).GetProperty("version").GetInt32();

        var vetTwo = await SignInStaffOnAsync(world.Api, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);
        var responses = await Task.WhenAll(
            world.Vet.PostAsJsonAsync($"/api/staff/visits/{visit.VisitId}/finalise", new { expectedVersion = expected }, Json),
            vetTwo.PostAsJsonAsync($"/api/staff/visits/{visit.VisitId}/finalise", new { expectedVersion = expected }, Json));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId);
        Assert.Equal(4, charges.Count);
        Assert.Equal(4, charges.Select(c => c.GetProperty("sourceLineRef").GetString()).Distinct().Count());
    }

    // ── J-4 recoverable ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task J4_AVisitThatWasNotCaptured_IsListed_AndTheManualCaptureRecordsItOnce()
    {
        var visit = await PrepareVisitAsync(WithoutVisitCaptureHandler);
        var world = visit.World;
        await FinaliseVisitAsync(world.Clinic, visit.VisitId);

        var today = DhakaDate(world.Clinic.Clock.GetUtcNow());
        var uncaptured = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        var entry = uncaptured.EnumerateArray().Single(e => e.GetProperty("sourceId").GetGuid() == visit.VisitId);
        Assert.Equal("Visit", entry.GetProperty("sourceKind").GetString());
        Assert.Equal(world.CustomerId, entry.GetProperty("customerId").GetGuid());

        var recovered = await CaptureAsync(world.Admin, "Visit", visit.VisitId);
        Assert.Equal(4, recovered.GetProperty("captured").GetInt32());
        Assert.Empty(recovered.GetProperty("skipped").EnumerateArray());
        Assert.Equal(0, (await CaptureAsync(world.Admin, "Visit", visit.VisitId)).GetProperty("captured").GetInt32());
    }

    [Fact]
    public async Task J4b_AnOpenVisit_IsNotBillable_AndAnUnknownOneIs404()
    {
        var world = await SeedPharmacyWorldAsync();

        var open = await CaptureAsync(world.Admin, "Visit", world.VisitId);

        Assert.Equal(0, open.GetProperty("captured").GetInt32());
        Assert.Equal("VisitNotFinalised", Assert.Single(open.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{Guid.NewGuid()}/capture", null)).StatusCode);
    }

    // ── J-5 the price on an order is frozen ──────────────────────────────────────────────────────

    [Fact]
    public async Task J5_ADiagnosticPriceChangedAfterTheOrder_DoesNotReprice_TheOrderAlreadyMade()
    {
        var world = await SeedPharmacyWorldAsync();
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceFelvFivTest, "500.00");
        await OrderDiagnosticAsync(world.Vet, world.VisitId, "FelvFivTest");
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceFelvFivTest, "900.00");

        await FinaliseVisitAsync(world.Clinic, world.VisitId);

        var charge = Assert.Single(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", world.VisitId));
        Assert.Equal(500m, Amount(charge));
        Assert.Equal("FeLV/FIV test", charge.GetProperty("description").GetString());
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceFelvFivTest, "0.00");
    }

    // ── J-6 dispensing ───────────────────────────────────────────────────────────────────────────

    private sealed record Rx(PharmacyWorld World, Guid SkuId, Guid PrescriptionId, Guid BatchId);

    private async Task<Rx> PrescribeAsync(decimal? price = 120m, decimal firstBatchQuantity = 6m)
    {
        var world = await SeedPharmacyWorldAsync();
        var sku = await CreateMedicineSkuAsync(world.Admin, price: price, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test dispense " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["meloxicam"], classes: ["nsaid"],
            dosingBasis: "Fixed", form: "OralLiquid", dispensingUnit: "Millilitre"));
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var issued = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions",
            PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: 10m), Json);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync());
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, firstBatchQuantity, BranchToday.AddDays(100));
        return new Rx(world, sku.SkuId, (await ReadAsync(issued)).GetProperty("prescriptionId").GetGuid(), batch);
    }

    private static Task<HttpResponseMessage> DispenseAsync(Rx rx, params (Guid BatchId, decimal Quantity)[] lines) =>
        rx.World.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{rx.PrescriptionId}/dispensings",
            new { lines = lines.Select(l => new { skuId = rx.SkuId, batchId = l.BatchId, quantity = l.Quantity }).ToArray() }, Json);

    [Fact]
    public async Task J6_ADispensing_IsBilledOncePerBatch_AtTheSnapshotPrice_AndAReversalVoidsWhatWasNotYetInvoiced()
    {
        var rx = await PrescribeAsync();
        var world = rx.World;
        var second = await ReceiveAsync(world.Admin, rx.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(300));

        var dispensed = await DispenseAsync(rx, (rx.BatchId, 6m), (second, 4m));
        Assert.True(dispensed.StatusCode == HttpStatusCode.Created, await dispensed.Content.ReadAsStringAsync());
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();

        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Dispense", dispensingId);
        Assert.Equal(2, charges.Count); // one per batch handed over
        Assert.All(charges, c =>
        {
            Assert.Equal("Medicine", c.GetProperty("itemKind").GetString());
            Assert.Equal("Clinic", c.GetProperty("businessLine").GetString());
            Assert.Equal(120m, Amount(c)); // the dispensing's snapshot
            Assert.Equal("Active", c.GetProperty("status").GetString());
            Assert.Equal(DhakaDate(world.Clinic.Clock.GetUtcNow()).ToString("yyyy-MM-dd"), c.GetProperty("serviceDate").GetString());
        });
        Assert.Equal([4m, 6m], charges.Select(Quantity).Order().ToArray());

        // A re-capture records nothing more.
        Assert.Equal(0, (await CaptureAsync(world.Admin, "Dispense", dispensingId)).GetProperty("captured").GetInt32());
        Assert.Equal(2, (await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Dispense", dispensingId)).Count);

        // The supply is reversed inside its window: the charges nobody has invoiced yet are voided, and a reversed supply is no longer billable.
        var reversed = await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse", new { reason = "Dispensed to the wrong cat" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);
        var after = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Dispense", dispensingId);
        Assert.Equal(2, after.Count);
        Assert.All(after, c => Assert.Equal("Voided", c.GetProperty("status").GetString()));

        var recapture = await CaptureAsync(world.Admin, "Dispense", dispensingId);
        Assert.Equal(0, recapture.GetProperty("captured").GetInt32());
        Assert.Equal("DispensingReversed", Assert.Single(recapture.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task J6b_ALineWithNoPriceRecorded_IsSkippedAndReported_NotPricedAtAGuess()
    {
        var rx = await PrescribeAsync(price: null, firstBatchQuantity: 20m);

        var dispensed = await DispenseAsync(rx, (rx.BatchId, 10m));
        Assert.True(dispensed.StatusCode == HttpStatusCode.Created, await dispensed.Content.ReadAsStringAsync());
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();

        Assert.Empty(await ChargesOfSourceAsync(rx.World.Admin, rx.World.CustomerId, "Dispense", dispensingId));
        var result = await CaptureAsync(rx.World.Admin, "Dispense", dispensingId);
        Assert.Equal(0, result.GetProperty("captured").GetInt32());
        Assert.Equal("SkuNotPriced", Assert.Single(result.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
    }

    // ── J-7 surgery ──────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid DrugMedicineId, Guid SkuId, Guid BatchId)> SeedSurgeryDrugAsync(SurgeryWorld world, decimal? price = 120m)
    {
        var sku = await CreateMedicineSkuAsync(world.Admin, price: price, priceEffectiveFrom: world.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test anaesthetic " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["propofol"], classes: ["sedative"],
            dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Vial", routes: ["Intravenous"]));
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(300));
        return (medicineId, sku.SkuId, batch);
    }

    private static async Task LogDrugAsync(SurgeryWorld world, Guid surgeryId, Guid medicineId, Guid batchId, decimal drawn)
    {
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs", new
        {
            medicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow(), administeredById = world.VetId,
            batchId, quantityDrawn = drawn, quantityWasted = 0m,
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task DischargeSurgeryAsync(SurgeryWorld world, Guid surgeryId, string outcome)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status = "ReadyForDischarge" }, Json)).StatusCode);
        var discharged = await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", new { }, Json);
        Assert.True(discharged.StatusCode == HttpStatusCode.OK, await discharged.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task J7_ADischargedSurgery_BillsTheProcedureFeeAndEachDrugUsed_Once()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, surgeryAppointmentId) = await InTheatreAsync(world);
        var drug = await SeedSurgeryDrugAsync(world);
        var unpriced = await SeedSurgeryDrugAsync(world, price: null);
        await LogDrugAsync(world, surgeryId, drug.DrugMedicineId, drug.BatchId, 2m);
        await LogDrugAsync(world, surgeryId, unpriced.DrugMedicineId, unpriced.BatchId, 1m);

        await DischargeSurgeryAsync(world, surgeryId, "Completed");

        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId);
        Assert.Equal(2, charges.Count); // the procedure fee and the one priced drug; the unpriced drug is reported, not guessed
        var fee = Assert.Single(charges, c => c.GetProperty("sourceLineRef").GetString() == "procedure");
        Assert.Equal("Service", fee.GetProperty("itemKind").GetString());
        Assert.Equal(5000m, Amount(fee)); // the surgery's price snapshot
        var medicine = Assert.Single(charges, c => c.GetProperty("sourceLineRef").GetString()!.StartsWith("drug:", StringComparison.Ordinal));
        Assert.Equal("Medicine", medicine.GetProperty("itemKind").GetString());
        Assert.Equal(120m, Amount(medicine));
        Assert.Equal(2m, Quantity(medicine));
        Assert.All(charges, c => Assert.Equal(DhakaDate(world.Clock.GetUtcNow()).ToString("yyyy-MM-dd"), c.GetProperty("serviceDate").GetString()));

        // The appointment of a surgery never completes by itself, so there is no second, appointment fee.
        var all = (await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/charges?customerId={world.CustomerId}&pageSize=200"))).GetProperty("items").EnumerateArray();
        Assert.DoesNotContain(all, c => c.GetProperty("sourceKind").GetString() == "Appointment");

        // The surgery source owns the surgery fee. The surgery's appointment is Completed by the surgery (CompleteSurgerySlot), but it is not on the uncaptured list
        // and a manual Appointment capture bills nothing (gate fix: one fee, one owning source).
        var today = DhakaDate(world.Clock.GetUtcNow());
        var uncaptured = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        Assert.DoesNotContain(uncaptured.EnumerateArray(), e => e.GetProperty("sourceId").GetGuid() == surgeryAppointmentId);
        var refused = await CaptureAsync(world.Admin, "Appointment", surgeryAppointmentId);
        Assert.Equal(0, refused.GetProperty("captured").GetInt32());
        Assert.Equal("AppointmentManagedBySurgery", Assert.Single(refused.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Appointment", surgeryAppointmentId));

        var again = await CaptureAsync(world.Admin, "Surgery", surgeryId);
        Assert.Equal(0, again.GetProperty("captured").GetInt32());
        Assert.Equal("SkuNotPriced", Assert.Single(again.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
        Assert.Equal(2, (await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId)).Count);
    }

    [Fact]
    public async Task J7b_AnAbandonedProcedure_IsBilledForTheDrugsUsed_NotTheProcedureFee()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedSurgeryDrugAsync(world);
        await LogDrugAsync(world, surgeryId, drug.DrugMedicineId, drug.BatchId, 1m);

        await DischargeSurgeryAsync(world, surgeryId, "Abandoned");

        var charge = Assert.Single(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId));
        Assert.StartsWith("drug:", charge.GetProperty("sourceLineRef").GetString());
    }

    [Fact]
    public async Task J7c_ASurgeryNotYetDischarged_IsNotBillable()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        var result = await CaptureAsync(world.Admin, "Surgery", surgeryId);

        Assert.Equal(0, result.GetProperty("captured").GetInt32());
        Assert.Equal("SurgeryNotDischarged", Assert.Single(result.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
    }

    // ── gate fix round ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task J2c_AnOwnerMergedTwice_AIntoBIntoC_IsBilledAsTheFinalSurvivor()
    {
        var visit = await PrepareVisitAsync(WithoutVisitCaptureHandler);
        var world = visit.World;
        await FinaliseVisitAsync(world.Clinic, visit.VisitId);
        var b = await OtherCustomerAsync(world);
        var c = await OtherCustomerAsync(world);
        await ExecuteAsync("UPDATE account.customer_account SET merged_into_customer_id = @s WHERE \"Id\" = @d", ("s", b), ("d", world.CustomerId));
        await ExecuteAsync("UPDATE account.customer_account SET merged_into_customer_id = @s WHERE \"Id\" = @d", ("s", c), ("d", b));

        Assert.Equal(4, (await CaptureAsync(world.Admin, "Visit", visit.VisitId)).GetProperty("captured").GetInt32());

        Assert.Equal(4, (await ChargesOfSourceAsync(world.Admin, c, "Visit", visit.VisitId)).Count);
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, b, "Visit", visit.VisitId));
    }

    [Fact]
    public async Task J1c_AVisitFinalisedAt2000Utc_IsServiceDatedTheNextDhakaDay()
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(20), TimeSpan.Zero);
        var visit = await PrepareVisitAsync(start: start);
        var world = visit.World;

        await FinaliseVisitAsync(world.Clinic, visit.VisitId);

        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visit.VisitId);
        Assert.Equal(4, charges.Count);
        // 20:00 UTC is 02:00 the NEXT day in Asia/Dhaka (UTC+6): the service date is not the UTC date.
        Assert.All(charges, c => Assert.Equal(DateOnly.FromDateTime(start.UtcDateTime).AddDays(1).ToString("yyyy-MM-dd"), c.GetProperty("serviceDate").GetString()));
    }

    [Fact]
    public async Task J4c_ANonBillableSourceOfAnotherBranch_IsSourceNotFound_NotItsState()
    {
        var mine = await SeedPharmacyWorldAsync();
        var other = await SeedPharmacyWorldAsync();

        // mine.VisitId is open (not billable): its own branch is told why, another branch is told nothing.
        Assert.Equal("VisitNotFinalised", Assert.Single((await CaptureAsync(mine.Admin, "Visit", mine.VisitId)).GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsync($"/api/staff/billing/sources/Visit/{mine.VisitId}/capture", null)).StatusCode);
    }

    [Fact]
    public async Task J4d_APartlyCapturedVisit_StaysOnTheUncapturedList_AndTheSkippedLineIsReported()
    {
        var world = await SeedPharmacyWorldAsync();
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m);
        var consumable = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments", new { kind = "Consumable", skuId = sku.SkuId, quantity = 1m, isBillable = true }, Json);
        Assert.True(consumable.StatusCode == HttpStatusCode.Created, await consumable.Content.ReadAsStringAsync());
        // The visit's service loses its tax class BEFORE the diagnostic is ordered, so the order freezes a class that does not exist; the consumable keeps its SKU's.
        await ExecuteAsync("UPDATE admin.clinic_service SET tax_class_id = @t WHERE \"Id\" = @s", ("t", Guid.NewGuid()), ("s", world.Clinic.ServiceId));
        await OrderDiagnosticAsync(world.Vet, world.VisitId, "Urinalysis");

        await FinaliseVisitAsync(world.Clinic, world.VisitId);

        Assert.Single(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", world.VisitId)); // the consumable only
        var today = DhakaDate(world.Clinic.Clock.GetUtcNow());
        var uncaptured = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        Assert.Contains(uncaptured.EnumerateArray(), e => e.GetProperty("sourceId").GetGuid() == world.VisitId); // one charge is fewer than two lines: still listed
        var retry = await CaptureAsync(world.Admin, "Visit", world.VisitId);
        Assert.Equal(0, retry.GetProperty("captured").GetInt32());
        Assert.Equal("TaxClassNotFound", Assert.Single(retry.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task J7d_ASurgeryBillsTheOwnerAtDischarge_NotTheBookingCustomer()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var newOwner = await OtherCustomerAsync(world.Pharmacy);
        await ExecuteAsync("UPDATE clinic.cat SET \"OwnerId\" = @o WHERE \"Id\" = @c", ("o", newOwner), ("c", world.CatId)); // the cat changed hands during the admission

        await DischargeSurgeryAsync(world, surgeryId, "Completed");

        Assert.Equal(newOwner, await ScalarAsync<Guid>("SELECT owner_at_discharge_id FROM clinic.surgery WHERE \"Id\" = @s", ("s", surgeryId)));
        Assert.Single(await ChargesOfSourceAsync(world.Admin, newOwner, "Surgery", surgeryId));
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId));
    }

    [Fact]
    public async Task J7e_ASurgeryDischargedAt2000Utc_IsServiceDatedTheNextDhakaDay()
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(20), TimeSpan.Zero);
        var world = await SeedSurgeryWorldAsync(start);
        var (surgeryId, _) = await InTheatreAsync(world);

        await DischargeSurgeryAsync(world, surgeryId, "Completed");

        var fee = Assert.Single(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId));
        Assert.Equal(DateOnly.FromDateTime(start.UtcDateTime).AddDays(1).ToString("yyyy-MM-dd"), fee.GetProperty("serviceDate").GetString());
    }

    // ── re-check R1 / R3 ─────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> LogDrugEntryAsync(SurgeryWorld world, Guid surgeryId, Guid medicineId, Guid batchId, decimal drawn)
    {
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs", new
        {
            medicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow(), administeredById = world.VetId,
            batchId, quantityDrawn = drawn, quantityWasted = 0m,
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await ReadAsync(response)).GetProperty("entryId").GetGuid();
    }

    private static async Task CorrectDrugAsync(SurgeryWorld world, Guid surgeryId, Guid entryId, decimal drawn)
    {
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{entryId}/corrections", new
        {
            reason = "Drew up less than first recorded", occurredOn = world.Clock.GetUtcNow(),
            drug = new { doseAmount = 0.4m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = drawn, quantityWasted = 0m },
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<bool> IsListedAsUncapturedAsync(SurgeryWorld world, Guid surgeryId)
    {
        var today = DhakaDate(world.Clock.GetUtcNow());
        var list = await ReadAsync(await world.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"));
        return list.EnumerateArray().Any(e => e.GetProperty("sourceId").GetGuid() == surgeryId);
    }

    [Fact]
    public async Task J7f_ACompletedSurgery_WithACorrectedUnpricedDrug_StaysListed_BecauseTheDrugLineWasNeverBilled()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var unpriced = await SeedSurgeryDrugAsync(world, price: null);
        var entry = await LogDrugEntryAsync(world, surgeryId, unpriced.DrugMedicineId, unpriced.BatchId, 2m);
        await CorrectDrugAsync(world, surgeryId, entry, 1m); // the original (2) is replaced by 1: the billable line is the CORRECTED one

        await DischargeSurgeryAsync(world, surgeryId, "Completed");

        var charge = Assert.Single(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId)); // the fee only: the drug has no price
        Assert.Equal("procedure", charge.GetProperty("sourceLineRef").GetString());
        Assert.True(await IsListedAsUncapturedAsync(world, surgeryId)); // 1 charge < fee + 1 corrected drug line
    }

    [Fact]
    public async Task J7g_AnAbandonedSurgery_WhoseOnlyDrugIsCorrected_AndWhoseCaptureSkippedIt_IsListed()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var unpriced = await SeedSurgeryDrugAsync(world, price: null);
        var entry = await LogDrugEntryAsync(world, surgeryId, unpriced.DrugMedicineId, unpriced.BatchId, 2m);
        await CorrectDrugAsync(world, surgeryId, entry, 1m);

        await DischargeSurgeryAsync(world, surgeryId, "Abandoned");

        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId)); // no fee for an abandoned procedure; the drug had no price
        Assert.True(await IsListedAsUncapturedAsync(world, surgeryId)); // nothing recorded, one drug line billable-by-count
    }

    [Fact]
    public async Task J7h_ADischargedSurgery_WithNothingToBill_IsNotListed()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        await DischargeSurgeryAsync(world, surgeryId, "Abandoned");

        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId));
        Assert.False(await IsListedAsUncapturedAsync(world, surgeryId));
    }

    [Fact]
    public async Task J7i_TheOwnerAtDischarge_IsFrozenInTheDatabaseOnceSet()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await DischargeSurgeryAsync(world, surgeryId, "Completed");
        var other = await OtherCustomerAsync(world.Pharmacy);

        var state = await SqlStateOfAsync("UPDATE clinic.surgery SET owner_at_discharge_id = @o WHERE \"Id\" = @s", ("o", other), ("s", surgeryId));

        Assert.Equal("CR001", state);
        Assert.Equal(world.CustomerId, await ScalarAsync<Guid>("SELECT owner_at_discharge_id FROM clinic.surgery WHERE \"Id\" = @s", ("s", surgeryId)));
    }
}
