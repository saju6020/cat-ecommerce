using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase E -- dispensing (US-PHM-05, US-PHM-04 AC2; BR-3, BR-4; ADR-143, ADR-147, ADR-154): I-E1 ... I-E13 and I-D9.</summary>
public sealed class PharmacyDispensingEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    private sealed record Stocked(PharmacyWorld World, Guid MedicineId, Guid SkuId, Guid PrescriptionId, string PrescriptionNumber);

    /// <summary>A world with a priced clinic-only SKU linked to a medicine, a weight, and an issued prescription for <paramref name="quantity"/> ml. Batches are received by the test.</summary>
    private async Task<Stocked> SeedAsync(bool controlled = false, decimal quantity = 10m, bool priced = true, DateTimeOffset? start = null, int repeats = 0)
    {
        var world = await SeedPharmacyWorldAsync(start);
        var sku = await CreateMedicineSkuAsync(world.Admin, price: priced ? 120m : null, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test dispense " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: [controlled ? "ketamine" : "meloxicam"], classes: [controlled ? "anaesthetic" : "nsaid"],
            dosingBasis: "Fixed", form: "OralLiquid", dispensingUnit: "Millilitre"));
        if (controlled) await SetControlledAsync(world.Admin, medicineId);
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);

        var (id, number) = await IssueAsync(world, medicineId, quantity, repeatable: repeats > 0, repeats: repeats, validUntil: repeats > 0 ? BranchToday.AddDays(90) : null);
        return new Stocked(world, medicineId, sku.SkuId, id, number);
    }

    private static async Task<(Guid Id, string Number)> IssueAsync(PharmacyWorld world, Guid medicineId, decimal quantity, Guid? catId = null, Guid? visitId = null, bool repeatable = false, int repeats = 0, DateOnly? validUntil = null)
    {
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{catId ?? world.CatId}/prescriptions",
            PharmacyPrescribingEndpointTests.IssueBody(visitId ?? world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: quantity, repeatable: repeatable, repeats: repeats, validUntil: validUntil), Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var body = await ReadAsync(response);
        return (body.GetProperty("prescriptionId").GetGuid(), body.GetProperty("prescriptionNumber").GetString()!);
    }

    private static object Line(Guid skuId, Guid batchId, decimal quantity) => new { skuId, batchId, quantity };

    private static Task<HttpResponseMessage> DispenseAsync(
        HttpClient client, Guid prescriptionId, object[] lines, string? fefoReason = null, Guid? repeatRequestId = null, string? witnessIdentifier = null, string? witnessPassword = null) =>
        client.PostAsJsonAsync($"/api/staff/prescriptions/{prescriptionId}/dispensings",
            new { lines, fefoOverrideReason = fefoReason, repeatRequestId, witnessIdentifier, witnessPassword }, Json);

    private static async Task<List<(Guid BatchId, decimal Delta, long Kind)>> LedgerForReferenceAsync(Guid referenceId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand("SELECT batch_id, quantity_delta, kind FROM inventory.stock_ledger_entry WHERE reference_id = @id ORDER BY sequence", connection);
        command.Parameters.AddWithValue("id", referenceId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(Guid, decimal, long)>();
        while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetInt32(2)));
        return rows;
    }

    // ── I-E1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E1_DispensingTheFefoSuggestion_TakesTheExactBatches_AndFreezesTheLabel()
    {
        var seeded = await SeedAsync(repeats: 2);
        var world = seeded.World;
        var early = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100), "EARLY-" + NewLotNumber());
        var late = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(300), "LATE-" + NewLotNumber());

        var plan = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan"));
        Assert.True(plan.GetProperty("canDispense").GetBoolean());
        var suggested = plan.GetProperty("suggestedLines").EnumerateArray().ToList();
        Assert.Equal(2, suggested.Count);
        Assert.Equal(early, suggested[0].GetProperty("batchId").GetGuid());
        Assert.Equal("6.00", suggested[0].GetProperty("quantity").GetProperty("amount").GetString());
        Assert.Equal(late, suggested[1].GetProperty("batchId").GetGuid());
        Assert.Equal("4.00", suggested[1].GetProperty("quantity").GetProperty("amount").GetString());
        Assert.False(plan.GetProperty("onlyExpiredStockAvailable").GetBoolean());
        Assert.Equal(2, plan.GetProperty("batches").GetArrayLength());

        var response = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, early, 6m), Line(seeded.SkuId, late, 4m)]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var dispensingId = body.GetProperty("dispensingId").GetGuid();
        Assert.Equal(1, body.GetProperty("supplyNumber").GetInt32());
        Assert.Empty(body.GetProperty("unpricedLines").EnumerateArray());

        // The label: cat, owner, medicine, dose line, instructions, date and the EARLIEST expiry.
        var label = body.GetProperty("label");
        Assert.Equal("Mitu", label.GetProperty("catName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(label.GetProperty("ownerName").GetString()));
        Assert.Contains("1.5 mg/ml", label.GetProperty("medicineLine").GetString());
        Assert.Equal("Give 2 ml by mouth once daily for 5 days", label.GetProperty("doseLine").GetString());
        Assert.Equal("Give with food.", label.GetProperty("instructions").GetString());
        Assert.Equal(BranchToday.AddDays(100).ToString("yyyy-MM-dd"), label.GetProperty("expiresOn").GetString());
        Assert.Equal("Quantity: 10 ml", label.GetProperty("quantityLine").GetString());
        Assert.Equal(seeded.PrescriptionNumber, label.GetProperty("prescriptionNumber").GetString());
        Assert.Contains("animal", label.GetProperty("standardWarnings").GetString());

        // The EXACT batches were deducted through Inventory's ledger, referenced by the dispensing id, carrying the recall stamp.
        var ledger = await LedgerForReferenceAsync(dispensingId);
        Assert.Equal(2, ledger.Count);
        Assert.Contains(ledger, l => l.BatchId == early && l.Delta == -6m && l.Kind == MovementKind.Dispense.Value);
        Assert.Contains(ledger, l => l.BatchId == late && l.Delta == -4m && l.Kind == MovementKind.Dispense.Value);
        Assert.Equal(0m, await OnHandAsync(early, world.DispensaryId));
        Assert.Equal(16m, await OnHandAsync(late, world.DispensaryId));
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @id AND customer_id = @c AND cat_id = @k",
            ("id", dispensingId), ("c", world.CustomerId), ("k", world.CatId)));

        var prescription = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        Assert.Equal("Dispensed", prescription.GetProperty("status").GetString());
        Assert.Equal(1, prescription.GetProperty("suppliesDispensed").GetInt32());
        Assert.Single(prescription.GetProperty("dispensings").EnumerateArray());

        // The dispensing reads back with the SAME frozen label, and the first print counts.
        var detail = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.Equal(label.GetProperty("doseLine").GetString(), detail.GetProperty("label").GetProperty("doseLine").GetString());
        Assert.Equal(0, detail.GetProperty("labelPrintCount").GetInt32());
        Assert.Contains("Reprint", detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.DoesNotContain("Reverse", detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
    }

    // ── I-E2 / I-E3: expired stock, no override ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E2_OnlyExpiredStockInTheDispensary_IsBlocked_NoLedgerEntry()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(30), "OLD-" + NewLotNumber());
        await SetBatchExpiryDirectlyAsync(batch, BranchToday.AddDays(-10));

        var plan = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan"));
        Assert.True(plan.GetProperty("onlyExpiredStockAvailable").GetBoolean());
        Assert.False(plan.GetProperty("canDispense").GetBoolean());
        Assert.Contains("expired", plan.GetProperty("blockingReason").GetString());
        var listed = Assert.Single(plan.GetProperty("batches").EnumerateArray());
        Assert.True(listed.GetProperty("isExpired").GetBoolean());
        Assert.False(listed.GetProperty("isDispensable").GetBoolean());
        Assert.Empty(plan.GetProperty("suggestedLines").EnumerateArray());

        var before = await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE batch_id = @b", ("b", batch));
        var response = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]);

        await AssertErrorAsync(response, "OnlyExpiredStockAvailable");
        Assert.Contains("It cannot be dispensed", await BodyAsync(response));
        Assert.Equal(before, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE batch_id = @b", ("b", batch)));
        Assert.Equal(20m, await OnHandAsync(batch, world.DispensaryId));
    }

    [Fact]
    public async Task I_E3_NamingAnExpiredBatch_WhileGoodStockExists_IsRefused_EvenForAnAdministrator()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var expired = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(30), "OLD-" + NewLotNumber());
        var good = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(200), "GOOD-" + NewLotNumber());
        await SetBatchExpiryDirectlyAsync(expired, BranchToday.AddDays(-1));

        // No role, no reason, no flag changes it (BR-3): the same answer as an administrator, with a reason attached.
        var admin = await DispenseAsync(world.Admin, seeded.PrescriptionId, [Line(seeded.SkuId, expired, 10m)], fefoReason: "Emergency, the owner is waiting");
        await AssertErrorAsync(admin, "BatchExpired");
        Assert.Contains("cannot be dispensed", await BodyAsync(admin));

        Assert.Equal(20m, await OnHandAsync(expired, world.DispensaryId));
        Assert.Equal(HttpStatusCode.Created, (await DispenseAsync(world.Admin, seeded.PrescriptionId, [Line(seeded.SkuId, good, 10m)])).StatusCode);
    }

    // ── I-E4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E4_ANonFefoBatch_NeedsAReason_AndTheReasonIsStored()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var early = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100), "EARLY-" + NewLotNumber());
        var late = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(300), "LATE-" + NewLotNumber());

        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, late, 10m)]), "FefoOverrideReasonRequired");
        Assert.Equal(20m, await OnHandAsync(late, world.DispensaryId));

        var accepted = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, late, 10m)], fefoReason: "The early box is damaged, sealed box taken");
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var dispensingId = (await ReadAsync(accepted)).GetProperty("dispensingId").GetGuid();

        var detail = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.Equal("The early box is damaged, sealed box taken", detail.GetProperty("fefoOverrideReason").GetString());
        Assert.Equal(10m, await OnHandAsync(late, world.DispensaryId));
        Assert.Equal(20m, await OnHandAsync(early, world.DispensaryId));
    }

    // ── shape rules of the planner, through the API ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheQuantityMustMatchExactly_TheSkuMustBeLinked_AndAShortDispensaryIsRefused()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100));

        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 5m)]), "DispenseQuantityMismatch");
        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(Guid.NewGuid(), batch, 10m)]), "SkuNotLinkedToMedicine");
        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]), "InsufficientDispensaryStock");
        Assert.Equal(HttpStatusCode.BadRequest, (await DispenseAsync(world.Nurse, seeded.PrescriptionId, [])).StatusCode);

        var tooMany = Enumerable.Range(0, 6).Select(_ => Line(seeded.SkuId, Guid.NewGuid(), 1m)).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await DispenseAsync(world.Nurse, seeded.PrescriptionId, tooMany)).StatusCode);
        Assert.Equal(6m, await OnHandAsync(batch, world.DispensaryId));
    }

    [Fact]
    public async Task AQuarantinedBatch_IsOfferedButNotDispensable_WithInventorysReason()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var quarantine = await world.Admin.PostAsJsonAsync($"/api/staff/inventory/batches/{batch}/quarantine", new { reason = "Inspection of the cold chain" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, quarantine.StatusCode);

        var plan = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan"));
        var listed = Assert.Single(plan.GetProperty("batches").EnumerateArray());
        Assert.False(listed.GetProperty("isDispensable").GetBoolean());
        Assert.Contains("quarantine", listed.GetProperty("notDispensableReason").GetString(), StringComparison.OrdinalIgnoreCase);
        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]), "BatchNotDispensable");
    }

    // ── I-E5 / I-E6: controlled drugs and the witness ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E5_ADispensedControlledDrug_NeedsAWitness_AndWritesOneRegisterEntryPerLine()
    {
        var seeded = await SeedAsync(controlled: true);
        var world = seeded.World;
        var early = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100));
        var late = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(300));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);
        object[] lines = [Line(seeded.SkuId, early, 6m), Line(seeded.SkuId, late, 4m)];

        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, lines), "WitnessRequired");
        Assert.Equal(6m, await OnHandAsync(early, world.DispensaryId));

        var response = await DispenseAsync(world.Nurse, seeded.PrescriptionId, lines, witnessIdentifier: witness.Mobile, witnessPassword: witness.Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        var dispensingId = created.GetProperty("dispensingId").GetGuid();
        var register = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}"));
        var entries = register.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("kind").GetString() == "Dispensed").ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal("Mitu", e.GetProperty("catName").GetString());
            Assert.False(string.IsNullOrEmpty(e.GetProperty("witnessName").GetString()));
            Assert.False(string.IsNullOrEmpty(e.GetProperty("actorName").GetString()));
            Assert.Contains("2 ml", e.GetProperty("doseText").GetString());
            Assert.Contains("RX", e.GetProperty("source").GetProperty("label").GetString());
            Assert.Equal(dispensingId, e.GetProperty("source").GetProperty("id").GetGuid());
        });
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE source_id = @id AND witness_id = @w", ("id", dispensingId), ("w", witness.StaffId)));
        Assert.Equal("Controlled drug", created.GetProperty("label").GetProperty("controlledNotice").GetString());
    }

    [Fact]
    public async Task I_E6_AReceptionistWitnessing_IsNotAWitness()
    {
        var seeded = await SeedAsync(controlled: true);
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var reception = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Reception);

        var response = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)], witnessIdentifier: reception.Mobile, witnessPassword: reception.Password);

        await AssertErrorAsync(response, "WitnessNotVerified");
        Assert.Equal(20m, await OnHandAsync(batch, world.DispensaryId));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE medicine_id = @m AND kind = @k", ("m", seeded.MedicineId), ("k", ControlledDrugEntryKind.Dispensed.Value)));
    }

    // ── I-E7 / I-E8: concurrency ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E7_ManyDispensersOnTheLastSupply_ExactlyOneWins_TheLedgerIsReducedOnce()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 100m, BranchToday.AddDays(100));
        var clients = new[] { world.Nurse, world.Vet, world.Reception, world.Admin }.ToList();
        var gate = new TaskCompletionSource();

        var calls = Enumerable.Range(0, 8).Select(async i =>
        {
            await gate.Task;
            return await DispenseAsync(clients[i % clients.Count], seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]);
        }).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(calls);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, created);
        // Whoever lost is told so: a 409 from the commit-time race, or a 400 NoSuppliesRemaining once the winner had committed -- never a second supply, never a 500.
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.True(r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, $"{(int)r.StatusCode}"));

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.dispensing WHERE prescription_id = @p", ("p", seeded.PrescriptionId)));
        Assert.Equal(90m, await OnHandAsync(batch, world.DispensaryId));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE batch_id = @b AND kind = @k", ("b", batch), ("k", MovementKind.Dispense.Value)));
    }

    [Fact]
    public async Task I_E8_TwoPrescriptionsTakingTheLastUnitsOfOneBatch_NeverMakeTheBalanceNegative()
    {
        var seeded = await SeedAsync(quantity: 10m);
        var world = seeded.World;
        var otherCat = await SeedCatAsync(world.CustomerId, "Second");
        var appointment = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, otherCat, world.Clinic.ServiceId);
        var otherVisit = await OpenVisitAsync(world.Nurse, appointment);
        await RecordWeightAsync(world.Vet, otherCat, 3.5m);
        var (secondId, _) = await IssueAsync(world, seeded.MedicineId, 10m, catId: otherCat, visitId: otherVisit);
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var gate = new TaskCompletionSource();

        var calls = new[] { seeded.PrescriptionId, secondId }.Select(async id =>
        {
            await gate.Task;
            return await DispenseAsync(world.Nurse, id, [Line(seeded.SkuId, batch, 10m)]);
        }).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(calls);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.True(r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, $"{(int)r.StatusCode}"));
        Assert.Equal(0m, await OnHandAsync(batch, world.DispensaryId));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_position WHERE batch_id = @b AND on_hand < 0", ("b", batch)));
    }

    // ── I-E9: all or nothing ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Wraps the real ledger and refuses a deduction for one SKU, as if a race took the stock after the first movement and the register entry were enlisted.</summary>
    private sealed class FailingLedger(IStockLedger inner, Guid failingSkuId, Guid? splitIntoBatch = null, int? failReturnAtCall = null) : IStockLedger
    {
        private int returnCalls;

        public Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct) => inner.PlanAsync(demand, ct);
        public Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) => inner.ReserveAsync(demand, reference, party, ct);
        public Task<StockResult<CatEcommerce.Shared.Common.Helpers.Unit>> ReleaseAsync(StockReference reference, CancellationToken ct) => inner.ReleaseAsync(reference, ct);
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct) => inner.ConsumeReservationAsync(reference, party, ct);

        public async Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
        {
            if (demand.SkuId == failingSkuId)
                return StockResult<IReadOnlyList<StockMovementLine>>.Failure("InsufficientStock", "A race took the stock.", "Quantity");

            var result = await inner.DeductAsync(demand, reference, party, ct);
            if (splitIntoBatch is not { } extraBatch || !result.IsSuccess) return result;

            // More movements than requested lines: each real movement comes back as two (all but one unit, then one unit from another batch).
            IReadOnlyList<StockMovementLine> split = result.Value!.SelectMany(m => new[] { m with { Quantity = m.Quantity - 1m }, m with { BatchId = extraBatch, LotNumber = "EXTRA", Quantity = 1m } }).ToList();
            return StockResult<IReadOnlyList<StockMovementLine>>.Success(split);
        }

        public Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct) =>
            Interlocked.Increment(ref returnCalls) == failReturnAtCall
                ? Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure("ReturnRefused", "The return was refused.", "Quantity"))
                : inner.ReceiveReturnAsync(stockReturn, reference, party, ct);
        public void RecordShortfall(StockShortfall shortfall) => inner.RecordShortfall(shortfall);
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct) => inner.AdjustAsync(adjustment, reference, party, ct);
        public Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => inner.GetBranchBalancesAsync(branchId, skuIds, ct);
        public Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => inner.GetDispensaryBatchesAsync(branchId, skuIds, ct);
        public Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct) => inner.GetClinicDispensaryAsync(branchId, ct);
        public Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct) => inner.ReserveBatchAsync(demand, reference, party, ct);
        public Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct) => inner.GetBatchUsabilityAsync(branchId, batchIds, ct);
    }

    [Fact]
    public async Task I_E9_WhenAnInventoryEnlistmentFailsAfterARegisterEntryWasEnlisted_NothingIsCommitted()
    {
        var world = await SeedPharmacyWorldAsync();
        var first = await CreateMedicineSkuAsync(world.Admin);
        var second = await CreateMedicineSkuAsync(world.Admin);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test atomic " + Guid.NewGuid().ToString("N")[..6], skuIds: [first.SkuId, second.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed", form: "OralLiquid",
            dispensingUnit: "Millilitre"));
        await SetControlledAsync(world.Admin, medicineId);
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var (prescriptionId, _) = await IssueAsync(world, medicineId, 10m);
        // Distinct expiries make the FEFO order across the two SKUs unambiguous: batchOne first.
        var batchOne = await ReceiveAsync(world.Admin, first.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100));
        var batchTwo = await ReceiveAsync(world.Admin, second.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(200));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);

        // The first SKU's movement and register entry ARE enlisted before the second SKU's deduction fails.
        var failing = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IStockLedger>(sp => new FailingLedger(sp.GetRequiredService<EfStockLedger>(), second.SkuId))));
        var nurse = await SignInStaffOnAsync(failing, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);

        var response = await DispenseAsync(nurse, prescriptionId, [Line(first.SkuId, batchOne, 6m), Line(second.SkuId, batchTwo, 4m)], witnessIdentifier: witness.Mobile, witnessPassword: witness.Password);

        await AssertErrorAsync(response, "InsufficientStock");
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.dispensing WHERE prescription_id = @p", ("p", prescriptionId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE batch_id = ANY(@b) AND kind = @k", ("b", new[] { batchOne, batchTwo }), ("k", MovementKind.Dispense.Value)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE medicine_id = @m", ("m", medicineId)));
        Assert.Equal(6m, await OnHandAsync(batchOne, world.DispensaryId));
        Assert.Equal(0, (await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{prescriptionId}"))).GetProperty("suppliesDispensed").GetInt32());
    }

    /// <summary>TEST gate (architecture hand-off) -- a reversal whose SECOND line's return fails, after the first line's return and register entry were enlisted and the dispensing
    /// and prescription were already mutated in memory, must leave everything exactly as the dispense left it.</summary>
    [Fact]
    public async Task WhenTheSecondReturnFailsDuringAReversal_NothingIsSaved_TheDispensingStaysDispensed()
    {
        var world = await SeedPharmacyWorldAsync();
        var first = await CreateMedicineSkuAsync(world.Admin);
        var second = await CreateMedicineSkuAsync(world.Admin);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test reverse atomic " + Guid.NewGuid().ToString("N")[..6], skuIds: [first.SkuId, second.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed",
            form: "OralLiquid", dispensingUnit: "Millilitre"));
        await SetControlledAsync(world.Admin, medicineId);
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var (prescriptionId, _) = await IssueAsync(world, medicineId, 10m);
        var batchOne = await ReceiveAsync(world.Admin, first.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(100));
        var batchTwo = await ReceiveAsync(world.Admin, second.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(200));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);

        var dispensed = await DispenseAsync(world.Nurse, prescriptionId, [Line(first.SkuId, batchOne, 6m), Line(second.SkuId, batchTwo, 4m)], witnessIdentifier: witness.Mobile, witnessPassword: witness.Password);
        Assert.Equal(HttpStatusCode.Created, dispensed.StatusCode);
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();
        var ledgerBefore = (await LedgerForReferenceAsync(dispensingId)).Count;
        var registerBefore = await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE medicine_id = @m", ("m", medicineId));
        Assert.Equal(2L, registerBefore);

        var failing = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IStockLedger>(sp => new FailingLedger(sp.GetRequiredService<EfStockLedger>(), Guid.Empty, failReturnAtCall: 2))));
        var vet = await SignInStaffOnAsync(failing, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);

        var response = await vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse",
            new { reason = "Wrong cat", witnessIdentifier = witness.Mobile, witnessPassword = witness.Password }, Json);

        await AssertErrorAsync(response, "ReturnRefused");
        Assert.Equal(ledgerBefore, (await LedgerForReferenceAsync(dispensingId)).Count);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @r AND kind = @k", ("r", dispensingId), ("k", MovementKind.Return.Value)));
        Assert.Equal(registerBefore, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE medicine_id = @m", ("m", medicineId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE medicine_id = @m AND kind = @k", ("m", medicineId), ("k", ControlledDrugEntryKind.Returned.Value)));
        var detail = await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.Equal("Dispensed", detail.GetProperty("status").GetString());
        Assert.Equal(1, (await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{prescriptionId}"))).GetProperty("suppliesDispensed").GetInt32());
    }

    /// <summary>CODE_REVIEW row 1 -- a ledger that answers with more movements than there were requested lines must still give every dispensing line its own id (it used to
    /// reuse the last line id and hit the primary key).</summary>
    [Fact]
    public async Task EveryMovementTheLedgerReturns_BecomesItsOwnDispensingLine_EvenWhenThereAreMoreMovementsThanRequestedLines()
    {
        var seeded = await SeedAsync(quantity: 10m);
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var other = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(200));
        var splitting = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IStockLedger>(sp => new FailingLedger(sp.GetRequiredService<EfStockLedger>(), Guid.Empty, splitIntoBatch: other))));
        var nurse = await SignInStaffOnAsync(splitting, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);

        var response = await DispenseAsync(nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var dispensingId = (await ReadAsync(response)).GetProperty("dispensingId").GetGuid();
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.dispensing_line WHERE dispensing_id = @d", ("d", dispensingId)));
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(DISTINCT \"Id\") FROM pharmacy.dispensing_line WHERE dispensing_id = @d", ("d", dispensingId)));
    }

    // ── I-E10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E10_ALabelPrintedTwice_IsCountedAndAuditedEachTime()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var dispensingId = (await ReadAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]))).GetProperty("dispensingId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PostAsync($"/api/staff/dispensings/{dispensingId}/label-prints", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsync($"/api/staff/dispensings/{dispensingId}/label-prints", null)).StatusCode);

        var detail = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.Equal(2, detail.GetProperty("labelPrintCount").GetInt32());

        var rows = (await AuditRowsAsync("DispensingAggregate", dispensingId)).Where(r => r.Action == "LabelPrinted").ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(AuditCategory.BusinessEvent, r.Category));
        Assert.Contains(rows, r => r.AfterValue!.Contains("PrintCount=2"));
        Assert.Equal(HttpStatusCode.NotFound, (await world.Nurse.PostAsync($"/api/staff/dispensings/{Guid.NewGuid()}/label-prints", null)).StatusCode);
    }

    // ── I-E11: reversal ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E11_AVetReversesWithinTheWindow_StockIsReturned_TheSupplyCountGoesBack_AndTheWindowThenCloses()
    {
        var seeded = await SeedAsync(controlled: true);
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);
        var dispensed = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)], witnessIdentifier: witness.Mobile, witnessPassword: witness.Password);
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();
        Assert.Equal(10m, await OnHandAsync(batch, world.DispensaryId));

        // A nurse may not reverse.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse", new { reason = "Wrong cat" }, Json)).StatusCode);

        // A controlled supply needs a witness to reverse too.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse", new { reason = "Wrong cat" }, Json), "WitnessRequired");

        var reversed = await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse",
            new { reason = "Dispensed to the wrong cat", witnessIdentifier = witness.Mobile, witnessPassword = witness.Password }, Json);
        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);

        // The returned medicine sits in the branch's Quarantine location (Inventory's rule for every return -- an inventory judgement decides what happens next); the dispensary was not touched.
        Assert.Equal(10m, await OnHandAsync(batch, world.QuarantineId));
        Assert.Equal(10m, await OnHandAsync(batch, world.DispensaryId));
        var ledger = await LedgerForReferenceAsync(dispensingId);
        Assert.Equal(2, ledger.Count);
        Assert.Contains(ledger, l => l.Kind == MovementKind.Return.Value && l.Delta == 10m);

        var register = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}"));
        Assert.Contains(register.GetProperty("items").EnumerateArray(), e => e.GetProperty("kind").GetString() == "Returned");

        var prescription = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        Assert.Equal(0, prescription.GetProperty("suppliesDispensed").GetInt32());
        Assert.Equal("Issued", prescription.GetProperty("status").GetString());
        var detail = await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.Equal("Reversed", detail.GetProperty("status").GetString());

        // A reversed supply cannot be reversed again, and the prescription can be supplied again (the supply number is free once more).
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse",
            new { reason = "Twice", witnessIdentifier = witness.Mobile, witnessPassword = witness.Password }, Json), "DispensingNotReversible");
        var again = await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)], witnessIdentifier: witness.Mobile, witnessPassword: witness.Password);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(1, (await ReadAsync(again)).GetProperty("supplyNumber").GetInt32());
    }

    [Fact]
    public async Task I_E11b_AfterTheReversalWindow_TheReversalIsRefused()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(200));
        var dispensingId = (await ReadAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]))).GetProperty("dispensingId").GetGuid();

        world.Clinic.Clock.Advance(TimeSpan.FromHours(25));
        await ReauthenticateAsync(world.Vet, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword);

        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse", new { reason = "Too late" }, Json), "ReversalWindowPassed");
        var detail = await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensings/{dispensingId}"));
        Assert.DoesNotContain("Reverse", detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
    }

    // ── I-E12: the event Billing consumes ───────────────────────────────────────────────────────────────────────────────────────

    private sealed class CapturingHandler(ConcurrentBag<PrescriptionDispensedEvent> captured) : IEventHandlerAsync<PrescriptionDispensedEvent>
    {
        public Task HandleAsync(PrescriptionDispensedEvent @event)
        {
            captured.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task I_E12_TheDispensedEvent_CarriesSkuBatchQuantityAndAPriceSnapshot_AnUnpricedSkuIsNullAndReported()
    {
        var captured = new ConcurrentBag<PrescriptionDispensedEvent>();
        var priced = await SeedAsync(priced: true);
        var world = priced.World;
        var host = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IEventHandlerAsync<PrescriptionDispensedEvent>>(_ => new CapturingHandler(captured))));
        var vet = await SignInStaffOnAsync(host, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);

        var batch = await ReceiveAsync(world.Admin, priced.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var response = await DispenseAsync(vet, priced.PrescriptionId, [Line(priced.SkuId, batch, 10m)]);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dispensingId = (await ReadAsync(response)).GetProperty("dispensingId").GetGuid();

        var raised = Assert.Single(captured, e => e.DispensingId == dispensingId);
        var line = Assert.Single(raised.Lines);
        Assert.Equal(priced.SkuId, line.SkuId);
        Assert.Equal(batch, line.BatchId);
        Assert.Equal(10m, line.Quantity);
        Assert.Equal(120m, line.UnitPrice);
        Assert.Equal("BDT", line.CurrencyCode);
        Assert.Equal(1, raised.SupplyNumber);

        // An unpriced SKU does not block care: the snapshot is null and the response says so.
        var unpriced = await SeedAsync(priced: false);
        var unpricedBatch = await ReceiveAsync(unpriced.World.Admin, unpriced.SkuId, unpriced.World.DispensaryId, 20m, BranchToday.AddDays(100));
        var second = await DispenseAsync(unpriced.World.Nurse, unpriced.PrescriptionId, [Line(unpriced.SkuId, unpricedBatch, 10m)]);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Single((await ReadAsync(second)).GetProperty("unpricedLines").EnumerateArray());
    }

    // ── I-E13 / I-D9: recall trace and the register against stock ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E13_TheRecallTrace_NamesTheCatAndCustomer_FromThePartyStampDispensingWrote_AndAReversedSupplyNetsToZero()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var dispensingId = (await ReadAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]))).GetProperty("dispensingId").GetGuid();

        var lot = await ScalarAsync<string>("SELECT lot_number FROM inventory.batch WHERE \"Id\" = @b", ("b", batch));
        var recall = await world.Admin.PostAsJsonAsync($"/api/staff/inventory/batches/{batch}/recall",
            new { confirmLotNumber = lot, reason = "Contamination found in the lot.", customerAdvice = "Stop giving it and call the clinic." }, Json);
        Assert.Equal(HttpStatusCode.Created, recall.StatusCode);

        var trace = await ReadAsync(await world.Admin.GetAsync($"/api/staff/inventory/recalls/{batch}/trace"));
        var received = Assert.Single(trace.GetProperty("received").EnumerateArray(), r => r.GetProperty("kind").GetString() == "Dispense");
        Assert.Equal("Mitu", received.GetProperty("cat").GetProperty("name").GetString());
        Assert.Equal(world.CustomerId, received.GetProperty("cat").GetProperty("currentOwner").GetProperty("customerId").GetGuid());

        // The stamp is on the ledger row itself.
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @id AND customer_id = @c AND cat_id = @k",
            ("id", dispensingId), ("c", world.CustomerId), ("k", world.CatId)));
    }

    [Fact]
    public async Task I_E13b_AReversedDispensing_NetsToZeroInTheLedger()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var dispensingId = (await ReadAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)]))).GetProperty("dispensingId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/dispensings/{dispensingId}/reverse", new { reason = "Wrong cat" }, Json)).StatusCode);

        var ledger = await LedgerForReferenceAsync(dispensingId);
        Assert.Equal(0m, ledger.Sum(l => l.Delta));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @id AND kind = @k AND customer_id IS NOT NULL AND cat_id IS NOT NULL",
            ("id", dispensingId), ("k", MovementKind.Return.Value)));
    }

    [Fact]
    public async Task I_D9_AfterDispensingAControlledDrug_RegisterAndStockBalancesAgree_AnInventoryOnlyAdjustmentOpensAMismatch()
    {
        var seeded = await SeedAsync(controlled: true);
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);

        // The opening balance enters the register through the published port (as HO-IMP-001's receipt of a controlled drug will).
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        await EnlistAsync(host, RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Receipt, 20m, world.Clinic.VetStaffId, skuId: seeded.SkuId, batchId: batch, name: "Test dispense"));
        Assert.Equal(HttpStatusCode.Created, (await DispenseAsync(world.Nurse, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)], witnessIdentifier: witness.Mobile, witnessPassword: witness.Password)).StatusCode);

        var balances = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/balances"));
        var row = Assert.Single(balances.EnumerateArray(), b => b.GetProperty("medicineId").GetGuid() == seeded.MedicineId);
        Assert.Equal("10.00", row.GetProperty("registerBalance").GetProperty("amount").GetString());
        Assert.Equal("10.00", row.GetProperty("stockBalance").GetProperty("amount").GetString());
        Assert.False(row.GetProperty("isMismatch").GetBoolean());

        var adjust = await world.Admin.PostAsJsonAsync("/api/staff/inventory/adjustments", new
        { locationId = world.DispensaryId, reason = "Damage", note = "Dropped a vial", lines = new[] { new { skuId = seeded.SkuId, batchId = batch, quantityDelta = -1m } } }, Json);
        Assert.True(adjust.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await BodyAsync(adjust));

        var after = Assert.Single((await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/balances"))).EnumerateArray(), b => b.GetProperty("medicineId").GetGuid() == seeded.MedicineId);
        Assert.True(after.GetProperty("isMismatch").GetBoolean());
        Assert.Equal("10.00", after.GetProperty("registerBalance").GetProperty("amount").GetString());
        Assert.Equal("9.00", after.GetProperty("stockBalance").GetProperty("amount").GetString());
    }

    // ── queue, lookup, who may do what ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheQueueAndTheLookup_ShowWhatAwaitsTheFirstSupply_AndAPrescriptionLeavesTheQueueOnceDispensed()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));

        var queue = await ReadAsync(await world.Reception.GetAsync("/api/staff/pharmacy/queue"));
        var item = Assert.Single(queue.GetProperty("awaitingFirstSupply").EnumerateArray(), i => i.GetProperty("prescription").GetProperty("id").GetGuid() == seeded.PrescriptionId);
        Assert.Equal("Mitu", item.GetProperty("catName").GetString());

        var lookup = await ReadAsync(await world.Reception.GetAsync($"/api/staff/pharmacy/lookup?number={seeded.PrescriptionNumber.ToLowerInvariant()}"));
        Assert.Equal(seeded.PrescriptionId, lookup.GetProperty("prescriptionId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await world.Reception.GetAsync("/api/staff/pharmacy/lookup?number=RX-0000-000000")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await DispenseAsync(world.Reception, seeded.PrescriptionId, [Line(seeded.SkuId, batch, 10m)])).StatusCode);

        var after = await ReadAsync(await world.Reception.GetAsync("/api/staff/pharmacy/queue"));
        Assert.DoesNotContain(after.GetProperty("awaitingFirstSupply").EnumerateArray(), i => i.GetProperty("prescription").GetProperty("id").GetGuid() == seeded.PrescriptionId);
    }

    [Fact]
    public async Task DispensingRoles_AreTheOnesADR153Names_AndACancelledOrUnknownPrescriptionIsRefused()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var batch = await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var inventory = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Inventory);
        var line = new object[] { Line(seeded.SkuId, batch, 10m) };

        // Inventory staff may look (Dispensing.View) but not dispense.
        Assert.Equal(HttpStatusCode.OK, (await inventory.Client.GetAsync("/api/staff/pharmacy/queue")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DispenseAsync(inventory.Client, seeded.PrescriptionId, line)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DispenseAsync(world.Nurse, Guid.NewGuid(), line)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/cancel", new { reason = "Prescribed in error" }, Json)).StatusCode);
        await AssertErrorAsync(await DispenseAsync(world.Nurse, seeded.PrescriptionId, line), "InvalidPrescriptionTransition");
        Assert.Equal(20m, await OnHandAsync(batch, world.DispensaryId));
    }

    // ── NFR-SEC-8: reading the dispense plan is clinical access ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadingTheDispensePlan_WritesOneClinicalAccessRow_NamingTheCat_AndAnUnknownPrescriptionWritesNone()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        await ReceiveAsync(world.Admin, seeded.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));
        var before = await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), seeded.PrescriptionId);

        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan")).StatusCode);
        Assert.Equal(before + 1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), seeded.PrescriptionId));

        Assert.Equal(HttpStatusCode.NotFound, (await world.Nurse.GetAsync($"/api/staff/prescriptions/{Guid.NewGuid()}/dispense-plan")).StatusCode);
        Assert.Equal(before + 1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), seeded.PrescriptionId));
    }

    [Fact]
    public async Task WhenTheAccessLogFails_TheDispensePlanIsRefusedWith500_AndNoClinicalDataIsReturned()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var failing = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, FailingAccessLogHandler>()));
        var nurse = await SignInStaffOnAsync(failing, world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword, world.BranchId);

        var response = await nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.DoesNotContain("Mitu", body);
        Assert.DoesNotContain(seeded.PrescriptionNumber, body);
        Assert.DoesNotContain(world.CatId.ToString(), body);
    }

    private sealed class FailingAccessLogHandler : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command) =>
            throw new InvalidOperationException("The access log is unavailable (test hook).");
    }
}
