using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-BIL-001 Phase J, TEST gate additions (cases JT-E1..E4 money maths, JT-F1..F3 overlapping races, JT-G1 diagnostic price freeze in the database). Same rules as
/// <see cref="BillingPhaseJEndpointTests"/>: every row of another area is made through that area's endpoints.
/// Money: tax is 0% at launch, so a line total is quantity x unit price. The expected figures are written as literals worked out by hand, never computed from the
/// response:
///   visit    = 1 x 500.00 (the visit service's price) + 2 x 120.00 + 1 x 350.00 (BloodWork)         = 1,090.00  (3 lines)
///   dispense = 6 x 85.50 + 4 x 85.50 = 513.00 + 342.00                                                = 855.00   (2 lines)
///   surgery  = 5,000.00 fee + 2 x 120.00 + (3 drawn, corrected to 1) x 45.50 = 5,000 + 240 + 45.50   = 5,285.50 (3 lines)
///   all      = 7,230.50 over 8 lines
/// </summary>
public sealed class BillingPhaseJGateTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
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

    private static decimal Dec(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
    private static decimal LineTotal(JsonElement charge) => Dec(charge.GetProperty("lineTotal").GetProperty("amount").GetString()!);
    private static decimal Unit(JsonElement charge) => Dec(charge.GetProperty("unitPrice").GetProperty("amount").GetString()!);
    private static decimal Qty(JsonElement charge) => Dec(charge.GetProperty("quantity").GetString()!);
    private static decimal Total(IEnumerable<JsonElement> charges) => charges.Sum(LineTotal);

    private static async Task<Guid> OrderDiagnosticAsync(HttpClient vet, Guid visitId, string kind)
    {
        var response = await vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind, description = (string?)null, analyteIds = Array.Empty<Guid>() }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("diagnosticOrderId").GetGuid();
    }

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A finalised visit: Procedure 1 x service price (500.00), Consumable 2 x 120.00, BloodWork diagnostic at 350.00.</summary>
    private async Task<(PharmacyWorld World, Guid VisitId)> FinalisedVisitAsync(Action<IServiceCollection>? configureServices = null, bool finalise = true)
    {
        var world = await SeedPharmacyWorldAsync(configureServices: configureServices);
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceBloodWork, "350.00");
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m);
        var consumable = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments", new { kind = "Consumable", skuId = sku.SkuId, quantity = 2m, isBillable = true }, Json);
        Assert.True(consumable.StatusCode == HttpStatusCode.Created, await consumable.Content.ReadAsStringAsync());
        var procedure = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments",
            new { kind = "Procedure", description = "Wound dressing", serviceId = world.Clinic.ServiceId, quantity = 1m, isBillable = true }, Json);
        Assert.True(procedure.StatusCode == HttpStatusCode.Created, await procedure.Content.ReadAsStringAsync());
        await OrderDiagnosticAsync(world.Vet, world.VisitId, "BloodWork");
        if (finalise) await FinaliseVisitAsync(world.Clinic, world.VisitId);
        return (world, world.VisitId);
    }

    private async Task<List<JsonElement>> DispensedBillAsync()
    {
        var world = await SeedPharmacyWorldAsync();
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 85.50m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Gate dispense " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["meloxicam"], classes: ["nsaid"],
            dosingBasis: "Fixed", form: "OralLiquid", dispensingUnit: "Millilitre"));
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var issued = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions",
            PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: 10m), Json);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync());
        var prescriptionId = (await ReadAsync(issued)).GetProperty("prescriptionId").GetGuid();
        var first = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100));
        var second = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(300));

        var dispensed = await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{prescriptionId}/dispensings",
            new { lines = new[] { new { skuId = sku.SkuId, batchId = first, quantity = 6m }, new { skuId = sku.SkuId, batchId = second, quantity = 4m } } }, Json);
        Assert.True(dispensed.StatusCode == HttpStatusCode.Created, await dispensed.Content.ReadAsStringAsync());
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();
        return await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Dispense", dispensingId);
    }

    private async Task<List<JsonElement>> DischargedSurgeryBillAsync()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        async Task<Guid> SeedDrugAsync(decimal price, decimal drawn)
        {
            var sku = await CreateMedicineSkuAsync(world.Admin, price: price, priceEffectiveFrom: world.Clock.GetUtcNow());
            var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
                name: "Gate anaesthetic " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["propofol"], classes: ["sedative"],
                dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Vial", routes: ["Intravenous"]));
            var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(300));
            var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs", new
            {
                medicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow(), administeredById = world.VetId,
                batchId = batch, quantityDrawn = drawn, quantityWasted = 0m,
            }, Json);
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return (await ReadAsync(response)).GetProperty("entryId").GetGuid();
        }

        await SeedDrugAsync(120m, 2m);                 // drug A: 2 x 120.00
        var drugB = await SeedDrugAsync(45.50m, 3m);   // drug B: drawn 3 ...
        var correction = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{drugB}/corrections", new
        {
            reason = "Drew up less than first recorded", occurredOn = world.Clock.GetUtcNow(),
            drug = new { doseAmount = 0.4m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = 1m, quantityWasted = 0m },
        }, Json);
        Assert.True(correction.StatusCode == HttpStatusCode.Created, $"{(int)correction.StatusCode}: {await correction.Content.ReadAsStringAsync()}"); // ... corrected to 1

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status = "ReadyForDischarge" }, Json)).StatusCode);
        var discharged = await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", new { }, Json);
        Assert.True(discharged.StatusCode == HttpStatusCode.OK, await discharged.Content.ReadAsStringAsync());
        return await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Surgery", surgeryId);
    }

    private async Task<List<JsonElement>> VisitBillAsync()
    {
        var (world, visitId) = await FinalisedVisitAsync();
        return await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId);
    }

    // ── JT-E money maths ────────────────────────────────────────────────────────────────────────

    private static void AssertVisitBill(List<JsonElement> charges)
    {
        Assert.Equal(3, charges.Count);
        Assert.Equal([240m, 350m, 500m], charges.Select(LineTotal).Order().ToArray());                  // 2 x 120, 1 x 350, 1 x 500
        Assert.Equal([1m, 1m, 2m], charges.Select(Qty).Order().ToArray());
        Assert.Equal(1090.00m, Total(charges));
    }

    private static void AssertDispenseBill(List<JsonElement> charges)
    {
        Assert.Equal(2, charges.Count);
        Assert.All(charges, c => Assert.Equal(85.50m, Unit(c)));
        Assert.Equal([4m, 6m], charges.Select(Qty).Order().ToArray());
        Assert.Equal([342.00m, 513.00m], charges.Select(LineTotal).Order().ToArray());                  // 4 x 85.50, 6 x 85.50
        Assert.Equal(855.00m, Total(charges));
    }

    private static void AssertSurgeryBill(List<JsonElement> charges)
    {
        Assert.Equal(3, charges.Count);
        Assert.Equal([45.50m, 240.00m, 5000.00m], charges.Select(LineTotal).Order().ToArray());          // drug B at 1 (not 3), drug A at 2, the fee
        Assert.Equal(5285.50m, Total(charges));
    }

    [Fact]
    public async Task JT_E1_AVisitWithTwoTreatmentsAndADiagnostic_TotalsExactly1090() => AssertVisitBill(await VisitBillAsync());

    [Fact]
    public async Task JT_E2_ADispensingOfTwoBatches_TotalsExactly855() => AssertDispenseBill(await DispensedBillAsync());

    [Fact]
    public async Task JT_E3_ASurgeryWithAFeeAndTwoDrugsOneCorrected_TotalsExactly5285_50() => AssertSurgeryBill(await DischargedSurgeryBillAsync());

    [Fact]
    public async Task JT_E4_TheThreeSourcesTogether_Total7230_50_Over8Lines()
    {
        var visit = await VisitBillAsync();
        var dispense = await DispensedBillAsync();
        var surgery = await DischargedSurgeryBillAsync();

        AssertVisitBill(visit);
        AssertDispenseBill(dispense);
        AssertSurgeryBill(surgery);
        var all = visit.Concat(dispense).Concat(surgery).ToList();
        Assert.Equal(8, all.Count);
        Assert.Equal(7230.50m, Total(all));
    }

    // ── JT-F overlapping races ──────────────────────────────────────────────────────────────────

    private sealed record Timed(long Start, long End, HttpResponseMessage Response);

    private static bool Overlap(Timed a, Timed b) => a.Start < b.End && b.Start < a.End;

    private static async Task<Timed[]> ReleaseTogetherAsync(params Func<Task<HttpResponseMessage>>[] calls)
    {
        var gate = new TaskCompletionSource();
        var ready = new CountdownEvent(calls.Length);
        var tasks = calls.Select(call => Task.Run(async () =>
        {
            ready.Signal();
            await gate.Task;
            var start = Stopwatch.GetTimestamp();
            var response = await call();
            return new Timed(start, Stopwatch.GetTimestamp(), response);
        })).ToList();
        ready.Wait();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task JT_F1_FinaliseThenTwoCapturesAtTheSameMoment_LeaveThreeLinesOnce_AndTheCallersReallyOverlapped()
    {
        var (world, visitId) = await FinalisedVisitAsync(); // the event capture has run
        Assert.Equal(3, (await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId)).Count);

        var overlapped = 0;
        for (var round = 0; round < 6; round++)
        {
            var calls = await ReleaseTogetherAsync(
                () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null),
                () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null));
            if (Overlap(calls[0], calls[1])) overlapped++;

            Assert.All(calls, c => Assert.True(c.Response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"round {round}: {(int)c.Response.StatusCode}"));
            foreach (var ok in calls.Where(c => c.Response.StatusCode == HttpStatusCode.OK))
                Assert.Equal(0, (await ReadAsync(ok.Response)).GetProperty("captured").GetInt32());
        }

        Assert.True(overlapped >= 1, $"no round had two requests in flight together ({overlapped}/6); the race was not exercised");
        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId);
        AssertVisitBill(charges);
        Assert.Equal(3, charges.Select(c => c.GetProperty("sourceLineRef").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task JT_F2_FinaliseThenTwoCapturesAndAnEventReplayAtTheSameMoment_LeaveThreeLinesOnce()
    {
        var (world, visitId) = await FinalisedVisitAsync();

        var overlapped = 0;
        for (var round = 0; round < 4; round++)
        {
            var calls = await ReleaseTogetherAsync(
                () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null),
                () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null),
                async () =>
                {
                    await using var scope = world.Api.Services.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetServices<IEventHandlerAsync<VisitFinalisedEvent>>().OfType<CaptureChargesOnVisitFinalisedHandler>().Single();
                    await handler.HandleAsync(new VisitFinalisedEvent
                    {
                        VisitId = visitId, AppointmentId = Guid.NewGuid(), CatId = world.CatId, BranchId = world.BranchId, CustomerId = world.CustomerId,
                        FinalisedBy = world.Clinic.VetStaffId, FinalisedOn = world.Clinic.Clock.GetUtcNow(),
                        UserContext = new Platform.Infrastructure.Common.Security.UserContext(world.Clinic.VetStaffId, Guid.NewGuid()), CorrelationId = Guid.NewGuid(),
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
            if (Overlap(calls[0], calls[1]) && Overlap(calls[0], calls[2])) overlapped++;
            Assert.All(calls, c => Assert.True(c.Response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"round {round}: {(int)c.Response.StatusCode}"));
            foreach (var ok in calls.Take(2).Where(c => c.Response.StatusCode == HttpStatusCode.OK))
                Assert.Equal(0, (await ReadAsync(ok.Response)).GetProperty("captured").GetInt32());
        }

        Assert.True(overlapped >= 1, $"no round had all three in flight together ({overlapped}/4)");
        AssertVisitBill(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId));
    }

    [Fact]
    public async Task JT_F3_TwoCapturesAtTheSameMoment_OfAVisitWithNoChargeYet_RecordEachLineOnce()
    {
        var (world, visitId) = await FinalisedVisitAsync(WithoutVisitCaptureHandler); // finalised, nothing captured: both callers have all 3 lines to insert
        Assert.Empty(await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId));

        var calls = await ReleaseTogetherAsync(
            () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null),
            () => world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null));

        Assert.All(calls, c => Assert.True(c.Response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"{(int)c.Response.StatusCode}"));
        Assert.Contains(calls, c => c.Response.StatusCode == HttpStatusCode.OK);
        Assert.True(Overlap(calls[0], calls[1]), "the two captures did not overlap in time");
        // A loser may stop early; one repeat completes the set. Whatever each was told, the book holds the 3 lines once.
        var repeat = await world.Admin.PostAsync($"/api/staff/billing/sources/Visit/{visitId}/capture", null);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var charges = await ChargesOfSourceAsync(world.Admin, world.CustomerId, "Visit", visitId);
        AssertVisitBill(charges);
        Assert.Equal(3, charges.Select(c => c.GetProperty("sourceLineRef").GetString()).Distinct().Count());
        var captured = 0;
        foreach (var ok in calls.Where(c => c.Response.StatusCode == HttpStatusCode.OK)) captured += (await ReadAsync(ok.Response)).GetProperty("captured").GetInt32();
        captured += (await ReadAsync(repeat)).GetProperty("captured").GetInt32();
        Assert.Equal(3, captured); // every line was reported as recorded by exactly one caller
    }

    // ── JT-G1 ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JT_G1_TheDatabaseRefusesToChangeADiagnosticOrdersPriceCurrencyOrTaxClass()
    {
        var world = await SeedPharmacyWorldAsync();
        await SetDiagnosticPriceAsync(world.Admin, SettingKeys.ClinicalDiagnosticPriceBloodWork, "350.00");
        var orderId = await OrderDiagnosticAsync(world.Vet, world.VisitId, "BloodWork");
        Assert.Equal(350.00m, await ScalarAsync<decimal>("SELECT unit_price_amount FROM clinic.diagnostic_order WHERE \"Id\" = @o", ("o", orderId)));

        Assert.Equal("CR001", await SqlStateOfAsync("UPDATE clinic.diagnostic_order SET unit_price_amount = 1.00 WHERE \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("UPDATE clinic.diagnostic_order SET unit_price_currency_code = 'USD' WHERE \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("UPDATE clinic.diagnostic_order SET tax_class_id = gen_random_uuid() WHERE \"Id\" = @o", ("o", orderId)));
        Assert.Equal(350.00m, await ScalarAsync<decimal>("SELECT unit_price_amount FROM clinic.diagnostic_order WHERE \"Id\" = @o", ("o", orderId)));
    }
}
