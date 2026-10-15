using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-EMR-001 section 16 Phase F -- treatments, consumables and stock (US-CLN-15; ADR-126, ADR-127): I-F1 ... I-F8 and the reads around them.
/// Real HTTP against the real Inventory: every branch, location, SKU, batch and receipt is made through HO-ADM-001, HO-SHP-001 and HO-IMP-001 own
/// endpoints (section 16.1 rule 5) -- a test never writes an <c>inventory.*</c> row. The only direct writes are the two the Inventory suite
/// itself makes: setting a received batch expiry into the past (a real receipt refuses expired goods) and, for I-F6, a real database trigger scoped
/// to this test own SKU that makes the ledger insert fail inside the commit.
/// </summary>
public sealed class TreatmentEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    /// <summary>A world with an open visit, the three stock locations and one priced clinic-only medicine SKU.</summary>
    private sealed record Stocked(PharmacyWorld World, MedicineSku Sku);

    private async Task<Stocked> SeedAsync(decimal? price = 120m, Action<IServiceCollection>? configureServices = null, DateTimeOffset? start = null)
    {
        var world = await SeedPharmacyWorldAsync(start, configureServices: configureServices);
        var sku = await CreateMedicineSkuAsync(world.Admin, price: price, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        return new Stocked(world, sku);
    }

    private static object Consumable(Guid skuId, decimal quantity, Guid? batchId = null, bool acceptShortfall = false, string? overrideReason = null, bool billable = false) => new
    {
        kind = "Consumable", skuId, quantity, batchId, acceptShortfall, batchOverrideReason = overrideReason, isBillable = billable
    };

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, Guid visitId, object body) =>
        client.PostAsJsonAsync($"/api/staff/visits/{visitId}/treatments", body, Json);

    private static string CodeOf(JsonElement body) => body.GetProperty("errors")[0].GetProperty("errorCode").GetString()!;

    private sealed record LedgerRow(Guid BatchId, decimal Delta, int Kind, Guid ReferenceId, Guid? ReferenceLineId, Guid? CatId, Guid? CustomerId, Guid LocationId, bool FefoOverridden);

    private static async Task<List<LedgerRow>> LedgerAsync(Guid skuId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT batch_id, quantity_delta, kind, reference_id, reference_line_id, cat_id, customer_id, location_id, fefo_overridden FROM inventory.stock_ledger_entry WHERE sku_id = @s ORDER BY sequence",
            connection);
        command.Parameters.AddWithValue("s", skuId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LedgerRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new LedgerRow(
                reader.GetGuid(0), reader.GetDecimal(1), reader.GetInt32(2), reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetGuid(7), reader.GetBoolean(8)));
        }

        return rows;
    }

    private static List<LedgerRow> Movements(List<LedgerRow> rows) => rows.Where(r => r.Kind != MovementKind.Receipt.Value).ToList();

    private async Task<JsonElement> VisitAsync(PharmacyWorld world) => await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{world.VisitId}"));

    // ── I-F1 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F1 -- a consumable with no batch named takes the FEFO first batch: one ClinicalUse ledger entry, minus the quantity, referring to the
    /// visit, the line, the cat and the cat owner (US-CLN-15 AC1, A10).</summary>
    [Fact]
    public async Task I_F1_AConsumableWithFefo_DeductsTheFefoBatch_AndRefersToTheLine()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200), "LATE-" + NewLotNumber());
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(40), "EARLY-" + NewLotNumber());

        var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 2m));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var body = await ReadAsync(response);
        var lineId = body.GetProperty("lineId").GetGuid();
        Assert.Equal("2", body.GetProperty("deductedQuantity").GetString());
        Assert.Equal("0", body.GetProperty("shortfallQuantity").GetString());
        Assert.Equal(early, body.GetProperty("batch").GetProperty("id").GetGuid());
        Assert.StartsWith("EARLY-", body.GetProperty("batch").GetProperty("lotNumber").GetString());

        var movement = Assert.Single(Movements(await LedgerAsync(sku.SkuId)));
        Assert.Equal(early, movement.BatchId);
        Assert.Equal(-2m, movement.Delta);
        Assert.Equal(MovementKind.ClinicalUse.Value, movement.Kind);
        Assert.Equal(world.VisitId, movement.ReferenceId);
        Assert.Equal(lineId, movement.ReferenceLineId);
        Assert.Equal(world.CatId, movement.CatId);
        Assert.Equal(world.CustomerId, movement.CustomerId);
        Assert.Equal(world.DispensaryId, movement.LocationId);
        Assert.False(movement.FefoOverridden);
        Assert.Equal(8m, await OnHandAsync(early, world.DispensaryId));

        // The visit carries the line, with its batch and the quantity as a measurement (UI-8).
        var visit = await VisitAsync(world);
        var line = Assert.Single(visit.GetProperty("treatments").EnumerateArray());
        Assert.Equal(lineId, line.GetProperty("id").GetGuid());
        Assert.Equal("Consumable", line.GetProperty("kind").GetString());
        Assert.Equal("Recorded", line.GetProperty("status").GetString());
        Assert.Equal("2", line.GetProperty("quantity").GetProperty("value").GetString());
        Assert.Equal("each", line.GetProperty("quantity").GetProperty("unit").GetString());
        Assert.Equal(early, line.GetProperty("batch").GetProperty("id").GetGuid());
        Assert.Contains("recordTreatment", visit.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
    }

    /// <summary>BR-4 -- a batch that is not the FEFO first needs a reason, and the ledger entry is flagged as an override.</summary>
    [Fact]
    public async Task ANonFefoBatch_NeedsAReason_AndTheLedgerEntryIsFlagged()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(40));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200));

        var refused = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m, batchId: late));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(BusinessViolationCodes.BatchOverrideReasonRequired, CodeOf(await ReadAsync(refused)));
        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));

        var accepted = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m, batchId: late, overrideReason: "The early lot is a different strength"));
        Assert.True(accepted.StatusCode == HttpStatusCode.Created, await BodyAsync(accepted));

        var movement = Assert.Single(Movements(await LedgerAsync(sku.SkuId)));
        Assert.Equal(late, movement.BatchId);
        Assert.True(movement.FefoOverridden);
        var line = (await VisitAsync(world)).GetProperty("treatments")[0];
        Assert.Equal("The early lot is a different strength", line.GetProperty("batchOverrideReason").GetString());
    }

    // ── I-F2 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F2 -- an expired batch is refused with no override (BR-3, FR-IMP-9): not with a reason, not at all; no ledger entry, no line.</summary>
    [Fact]
    public async Task I_F2_AnExpiredBatch_IsRefused_AndNothingIsWritten()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200));
        var expired = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(30), "OLD-" + NewLotNumber());
        await SetBatchExpiryDirectlyAsync(expired, BranchToday.AddDays(-2));

        foreach (var reason in new string?[] { null, "The owner is waiting and it is only two days out of date" })
        {
            var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m, batchId: expired, overrideReason: reason));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await ReadAsync(response);
            Assert.Equal(BusinessViolationCodes.BatchExpired, CodeOf(body));
            Assert.Contains("OLD-", body.GetProperty("errors")[0].GetProperty("errorMessage").GetString());
        }

        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
        Assert.Empty((await VisitAsync(world)).GetProperty("treatments").EnumerateArray());
        Assert.Equal(10m, await OnHandAsync(expired, world.DispensaryId));

        // The batch picker does not offer it.
        var batches = await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensary/items/{sku.SkuId}/batches"));
        Assert.DoesNotContain(batches.EnumerateArray(), b => b.GetProperty("batchId").GetGuid() == expired);
    }

    // ── I-F3 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F3 -- more than the batch holds: first a warning with what is there, then (acceptShortfall) the treatment is recorded, what is
    /// there is deducted and the rest is a shortfall Inventory is told about (US-CLN-15 AC2, A11).</summary>
    [Fact]
    public async Task I_F3_OverStock_IsRefusedWithTheAvailableQuantity_ThenRecordedWithAShortfall()
    {
        var (world, sku) = await SeedAsync();
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(100));

        var warned = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 5m));
        Assert.Equal(HttpStatusCode.BadRequest, warned.StatusCode);
        var warning = await ReadAsync(warned);
        Assert.Equal(BusinessViolationCodes.InsufficientStock, CodeOf(warning));
        Assert.Equal("3", warning.GetProperty("availableQuantity").GetString());
        Assert.Equal("each", warning.GetProperty("unit").GetString());
        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
        Assert.Equal(0, await AlertCountAsync(sku.SkuId));

        var recorded = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 5m, acceptShortfall: true));
        Assert.True(recorded.StatusCode == HttpStatusCode.Created, await BodyAsync(recorded));
        var body = await ReadAsync(recorded);
        Assert.Equal("3", body.GetProperty("deductedQuantity").GetString());
        Assert.Equal("2", body.GetProperty("shortfallQuantity").GetString());

        var movement = Assert.Single(Movements(await LedgerAsync(sku.SkuId)));
        Assert.Equal(-3m, movement.Delta);
        Assert.Equal(0m, await OnHandAsync(batch, world.DispensaryId));

        // US-CLN-15 AC2 "record the shortfall": a Shortfall alert for Inventory's reorder list, committed with the line.
        Assert.Equal(1, await AlertCountAsync(sku.SkuId));
        Assert.Equal(2m, await ScalarAsync<decimal>(
            "SELECT quantity_at_raise FROM inventory.stock_alert WHERE sku_id = @s AND kind = @k", ("s", sku.SkuId), ("k", StockAlertKind.Shortfall.Value)));

        var line = (await VisitAsync(world)).GetProperty("treatments")[0];
        Assert.Equal("5", line.GetProperty("quantity").GetProperty("value").GetString());
        Assert.Equal("3", line.GetProperty("deductedQuantity").GetProperty("value").GetString());
        Assert.Equal("2", line.GetProperty("shortfallQuantity").GetProperty("value").GetString());
    }

    private static async Task<int> AlertCountAsync(Guid skuId) =>
        await ScalarAsync<int>("SELECT COUNT(*) FROM inventory.stock_alert WHERE sku_id = @s AND kind = @k", ("s", skuId), ("k", StockAlertKind.Shortfall.Value));

    /// <summary>U-F6 end to end -- an item that has run out entirely can still be found and recorded as a shortfall.</summary>
    [Fact]
    public async Task AnItemWithNoStockAtAll_CanBeSearched_AndRecordedAsAWholeShortfall()
    {
        var (world, sku) = await SeedAsync();

        var items = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensary/items?search={Uri.EscapeDataString(sku.Name)}"))).GetProperty("items");
        var found = Assert.Single(items.EnumerateArray(), i => i.GetProperty("skuId").GetGuid() == sku.SkuId);
        Assert.Equal("0", found.GetProperty("availableQuantity").GetString());

        var recorded = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 2m, acceptShortfall: true));

        Assert.True(recorded.StatusCode == HttpStatusCode.Created, await BodyAsync(recorded));
        var body = await ReadAsync(recorded);
        Assert.Equal("0", body.GetProperty("deductedQuantity").GetString());
        Assert.Equal("2", body.GetProperty("shortfallQuantity").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("batch").ValueKind);
        Assert.Empty(await LedgerAsync(sku.SkuId));
        Assert.Equal(1, await AlertCountAsync(sku.SkuId));
    }

    // ── I-F4 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Holds each caller at the moment it asks the ledger to deduct, until both have arrived: both have then already read "one unit is
    /// there", so the race for the last unit is certain, not a matter of timing.</summary>
    private sealed class DeductGate
    {
        private int _arrived;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitForBothAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= 2) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class GatedLedger(IStockLedger inner, DeductGate? gate, DeductGate? returnGate = null) : IStockLedger
    {
        public async Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
        {
            if (gate is not null) await gate.WaitForBothAsync();
            return await inner.DeductAsync(demand, reference, party, ct);
        }

        public Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct) => inner.PlanAsync(demand, ct);
        public Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) => inner.ReserveAsync(demand, reference, party, ct);
        public Task<StockResult<CatEcommerce.Shared.Common.Helpers.Unit>> ReleaseAsync(StockReference reference, CancellationToken ct) => inner.ReleaseAsync(reference, ct);
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct) => inner.ConsumeReservationAsync(reference, party, ct);
        public async Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct)
        {
            if (returnGate is not null) await returnGate.WaitForBothAsync();
            return await inner.ReceiveReturnAsync(stockReturn, reference, party, ct);
        }
        public Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct) => inner.ReserveBatchAsync(demand, reference, party, ct);
        public Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct) => inner.GetBatchUsabilityAsync(branchId, batchIds, ct);
        public void RecordShortfall(StockShortfall shortfall) => inner.RecordShortfall(shortfall);
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct) => inner.AdjustAsync(adjustment, reference, party, ct);
        public Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => inner.GetBranchBalancesAsync(branchId, skuIds, ct);
        public Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => inner.GetDispensaryBatchesAsync(branchId, skuIds, ct);
        public Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct) => inner.GetClinicDispensaryAsync(branchId, ct);
        public Task<StockResult<IReadOnlyList<Guid>>> GetDispensarySkuIdsAsync(Guid branchId, CancellationToken ct) => inner.GetDispensarySkuIdsAsync(branchId, ct);
    }

    /// <summary>I-F4 -- two vets record the last unit at the same moment (two visits of two cats at one branch). Both pass the availability check;
    /// the ledger commit lets one through and the projection CHECK refuses the other, which is a 409 InsufficientStock (IMP-5): one 201, one 409,
    /// and the shelf is at zero, not below.</summary>
    [Fact]
    public async Task I_F4_TwoRequestsForTheLastUnit_OneWins_OneGets409()
    {
        var gate = new DeductGate();
        var (world, sku) = await SeedAsync(configureServices: services =>
            services.AddScoped<IStockLedger>(sp => new GatedLedger(sp.GetRequiredService<EfStockLedger>(), gate)));
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 1m, BranchToday.AddDays(100));
        var secondVisit = await OpenAnotherVisitAsync(world);

        var results = await Task.WhenAll(
            RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m)),
            RecordAsync(world.Vet, secondVisit, Consumable(sku.SkuId, 1m)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        var loser = Assert.Single(results, r => r.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Equal(BusinessViolationCodes.InsufficientStock, CodeOf(await ReadAsync(loser))); // TG-6: the commit-time CHECK path, mapped

        Assert.Equal(0m, await OnHandAsync(batch, world.DispensaryId));
        Assert.Single(Movements(await LedgerAsync(sku.SkuId)));

        // Nothing of the loser was committed: exactly one of the two visits carries a line.
        var lines = new[] { world.VisitId, secondVisit }.Select(async id => (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{id}"))).GetProperty("treatments").GetArrayLength());
        Assert.Equal(1, (await Task.WhenAll(lines)).Sum());
    }

    // ── I-F5 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F5 -- reversing a line writes the compensating entry (+quantity, Return, referring to the same line) and marks the line; a second
    /// reversal is refused. The units land in the branch Quarantine location, not back in the dispensary (HO-IMP-001 rule 8, OQ-19).</summary>
    [Fact]
    public async Task I_F5_Reversing_WritesTheCompensatingEntry_AndMarksTheLine()
    {
        var (world, sku) = await SeedAsync();
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var recorded = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 4m)));
        var lineId = recorded.GetProperty("lineId").GetGuid();

        var reversed = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "Wrong item chosen" }, Json);

        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);
        var movements = Movements(await LedgerAsync(sku.SkuId));
        Assert.Equal(2, movements.Count);
        var back = movements[1];
        Assert.Equal(4m, back.Delta);
        Assert.Equal(MovementKind.Return.Value, back.Kind);
        Assert.Equal(batch, back.BatchId);
        Assert.Equal(world.VisitId, back.ReferenceId);
        Assert.Equal(lineId, back.ReferenceLineId);
        Assert.Equal(world.QuarantineId, back.LocationId);
        // The vet reason is clinical free text: it stays on the visit and is never copied into a stock table.
        Assert.DoesNotContain("Wrong item chosen", await ScalarAsync<string>("SELECT note FROM inventory.stock_ledger_entry WHERE reference_line_id = @l AND quantity_delta > 0", ("l", lineId)) ?? "");
        Assert.Equal(6m, await OnHandAsync(batch, world.DispensaryId));
        Assert.Equal(4m, await OnHandAsync(batch, world.QuarantineId));

        var line = Assert.Single((await VisitAsync(world)).GetProperty("treatments").EnumerateArray());
        Assert.Equal("Reversed", line.GetProperty("status").GetString());
        Assert.Equal("Wrong item chosen", line.GetProperty("reversalReason").GetString());

        var again = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "Again" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(BusinessViolationCodes.TreatmentAlreadyReversed, CodeOf(await ReadAsync(again)));
        Assert.Equal(2, Movements(await LedgerAsync(sku.SkuId)).Count);

        var unknown = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{Guid.NewGuid()}/reverse", new { reason = "x" }, Json);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // ── I-F6 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F6 -- the ledger insert fails inside the commit (a real trigger scoped to this test own SKU): neither the line nor the ledger
    /// entry is committed (ADR-126 atomicity), and the shelf is untouched.</summary>
    [Fact]
    public async Task I_F6_AFailedSave_LeavesNeitherTheLineNorTheEntry()
    {
        var (world, sku) = await SeedAsync();
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using (var connection = await OpenConnectionAsync())
        {
            await using var create = new NpgsqlCommand($$"""
                create function public.test_block_ledger_{{suffix}}() returns trigger language plpgsql as
                $f$ begin raise exception 'forced ledger failure' using errcode = 'P0001'; end $f$;
                create trigger tr_test_block_ledger_{{suffix}} before insert on inventory.stock_ledger_entry
                    for each row when (NEW.sku_id = '{{sku.SkuId}}' and NEW.quantity_delta < 0) execute function public.test_block_ledger_{{suffix}}();
                """, connection);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 2m));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.visit_treatment where visit_id = @v", ("v", world.VisitId)));
            Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
            Assert.Equal(10m, await OnHandAsync(batch, world.DispensaryId));
        }
        finally
        {
            await using var connection = await OpenConnectionAsync();
            await using var drop = new NpgsqlCommand(
                $"drop trigger tr_test_block_ledger_{suffix} on inventory.stock_ledger_entry; drop function public.test_block_ledger_{suffix}();", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    // ── I-F7 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F7 -- a controlled drug is refused (ADR-126): the SKU is linked to a formulary medicine flagged controlled (HO-PHM-001), and nothing moves.</summary>
    [Fact]
    public async Task I_F7_AControlledSku_IsRefused()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test ketamine " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed"));
        await SetControlledAsync(world.Admin, medicineId);

        var response = await RecordAsync(world.Vet, world.VisitId, new { kind = "Medicine", skuId = sku.SkuId, quantity = 1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BusinessViolationCodes.ControlledDrugNotSupportedHere, CodeOf(await ReadAsync(response)));
        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));

        // The dispensary list shows it, flagged, so the client can say why it cannot be chosen.
        var items = (await ReadAsync(await world.Vet.GetAsync("/api/staff/dispensary/items"))).GetProperty("items");
        var listed = Assert.Single(items.EnumerateArray(), i => i.GetProperty("skuId").GetGuid() == sku.SkuId);
        Assert.True(listed.GetProperty("isControlledDrug").GetBoolean());
    }

    // ── I-F8 ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F8 -- HO-BIL-001's contract: after finalisation, GetVisitChargeLinesQuery returns the billable, non-reversed lines with the price and tax
    /// class taken when each was recorded; an open visit returns nothing (a line can still be reversed). The summary carries the non-reversed
    /// lines and the medicines.</summary>
    [Fact]
    public async Task I_F8_ChargeLines_AreTheBillableNonReversedLines_WithTheirSnapshots()
    {
        // Opened at 17:50 UTC (23:50 in Dhaka) and finalised 20 minutes later (00:10 the next day there): the service date is the FINALISED local date.
        var opened = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(17).AddMinutes(50), TimeSpan.Zero);
        var (world, sku) = await SeedAsync(price: 120m, start: opened);
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(100));

        var billable = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Medicine", skuId = sku.SkuId, quantity = 3m, isBillable = true, dose = "0.3 ml", route = "Oral" }));
        var billableLine = billable.GetProperty("lineId").GetGuid();
        var free = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Consumable", skuId = sku.SkuId, quantity = 1m, isBillable = false }));
        var mistake = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Consumable", skuId = sku.SkuId, quantity = 2m, isBillable = true }));
        var mistakeLine = mistake.GetProperty("lineId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{mistakeLine}/reverse", new { reason = "Entered twice" }, Json)).StatusCode);
        var procedure = await RecordAsync(world.Vet, world.VisitId, new { kind = "Procedure", description = "Scale and polish", serviceId = world.Clinic.ServiceId, quantity = 1m, isBillable = true });
        Assert.True(procedure.StatusCode == HttpStatusCode.Created, await BodyAsync(procedure));
        var procedureLine = (await ReadAsync(procedure)).GetProperty("lineId").GetGuid();

        Assert.Null(await ChargeLinesAsync(world, world.VisitId));

        world.Clinic.Clock.Advance(TimeSpan.FromMinutes(20));
        await FinaliseAsync(world);

        var charges = await ChargeLinesAsync(world, world.VisitId);
        Assert.NotNull(charges);
        Assert.Equal(world.VisitId, charges!.VisitId);
        Assert.Equal(world.CatId, charges.CatId);
        Assert.Equal(world.CustomerId, charges.CustomerId);
        Assert.Equal(world.BranchId, charges.BranchId);
        var timeZoneId = (await VisitAsync(world)).GetProperty("branchTimeZoneId").GetString()!;
        Assert.Equal("Asia/Dhaka", timeZoneId);
        var finalisedLocal = DateOnly.FromDateTime(opened.AddMinutes(20).UtcDateTime).AddDays(1); // 00:10 Dhaka is the next calendar day
        var openedLocal = DateOnly.FromDateTime(opened.UtcDateTime);                                // 23:50 Dhaka is still the UTC date
        Assert.Equal(finalisedLocal, charges.ServiceDate);
        Assert.NotEqual(openedLocal, charges.ServiceDate);
        Assert.NotEqual(DateOnly.FromDateTime(charges.FinalisedOn.UtcDateTime), charges.ServiceDate);
        Assert.Equal(2, charges.Lines.Count);

        var medicine = Assert.Single(charges.Lines, l => l.LineId == billableLine);
        Assert.Equal("Medicine", medicine.Kind);
        Assert.Equal("3", medicine.Quantity);
        Assert.Equal("each", medicine.Unit);
        Assert.Equal("120.00", medicine.UnitPrice.Amount);
        Assert.Equal("BDT", medicine.UnitPrice.CurrencyCode);
        Assert.Equal(sku.SkuId, medicine.SkuId);
        Assert.Null(medicine.ServiceId);
        Assert.NotEqual(Guid.Empty, medicine.TaxClassId);

        var service = Assert.Single(charges.Lines, l => l.LineId == procedureLine);
        Assert.Equal("Procedure", service.Kind);
        Assert.Equal(world.Clinic.ServiceId, service.ServiceId);
        Assert.Null(service.SkuId);
        Assert.NotEqual(Guid.Empty, service.TaxClassId);

        Assert.DoesNotContain(charges.Lines, l => l.LineId == mistakeLine);
        Assert.DoesNotContain(charges.Lines, l => l.LineId == free.GetProperty("lineId").GetGuid());

        // The discharge summary lists the non-reversed lines and, separately, the medicines -- no price, batch or staff name.
        var summary = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{world.VisitId}/discharge-summary"));
        var treatments = summary.GetProperty("treatments").EnumerateArray().Select(t => t.GetString()!).ToList();
        Assert.Equal(3, treatments.Count);
        Assert.Contains(treatments, t => t.Contains("0.3 ml") && t.Contains("Oral") && t.Contains("(3 each)"));
        Assert.Contains(treatments, t => t.StartsWith("Scale and polish"));
        var medicines = summary.GetProperty("medicinesAdministered").EnumerateArray().Select(t => t.GetString()!).ToList();
        Assert.Single(medicines);
        Assert.Contains("0.3 ml", medicines[0]);

        var pdf = await world.Vet.GetAsync($"/api/staff/visits/{world.VisitId}/discharge-summary.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);

        // A finalised visit takes no further treatment and cannot be reversed (the triggers back this up -- I-C9).
        var late = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Equal(BusinessViolationCodes.VisitFinalised, CodeOf(await ReadAsync(late)));
        var lateReverse = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{billableLine}/reverse", new { reason = "Too late" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, lateReverse.StatusCode);
        Assert.Equal(BusinessViolationCodes.VisitFinalised, CodeOf(await ReadAsync(lateReverse)));
    }

    private static async Task<VisitChargeLinesViewModel?> ChargeLinesAsync(PharmacyWorld world, Guid visitId)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var response = await dispatcher.GetResultAsync(new GetVisitChargeLinesQuery { VisitId = visitId, UserContext = new(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid() });
        return response.Result;
    }

    private async Task FinaliseAsync(PharmacyWorld world)
    {
        var codeId = await CreateDiagnosisCodeAsync(world.Vet);
        var version = (await VisitAsync(world)).GetProperty("version").GetInt32();
        var saved = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{world.VisitId}/note", new
        {
            expectedVersion = version, assessment = "Otitis externa", plan = "Ear drops", dischargeInstructions = "Keep the ear dry.",
            problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/finalise", new { expectedVersion = (await VisitAsync(world)).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalised.StatusCode);
    }

    // ── the guards and the reads ────────────────────────────────────────────────────────────────

    /// <summary>Treatments are a vet write (Visits.WriteNote, OQ-4): a nurse and reception are refused, and the dispensary reads follow the same rule.</summary>
    [Fact]
    public async Task OnlyAVetRecordsTreatments_AndReadsTheDispensary()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(100));

        foreach (var client in new[] { world.Nurse, world.Reception })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await RecordAsync(client, world.VisitId, Consumable(sku.SkuId, 1m))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/dispensary/items")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/staff/dispensary/items/{sku.SkuId}/batches")).StatusCode);
        }

        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
    }

    /// <summary>A visit of another branch is a 404 (Amended (4)), never a refusal that confirms it exists.</summary>
    [Fact]
    public async Task AVisitOfAnotherBranch_IsNotFound_ForTreatments()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(100));
        var other = await SeedPharmacyWorldAsync(openVisit: false);

        var response = await RecordAsync(other.Vet, world.VisitId, Consumable(sku.SkuId, 1m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
    }

    /// <summary>The dispensary items list shows the SKUs the branch dispensary holds, with the quantity Inventory would let a vet use; the batch list
    /// is FEFO with the default marked, and offers neither an empty batch nor one held at another branch dispensary.</summary>
    [Fact]
    public async Task TheDispensaryReads_ListStockedItems_AndFefoBatches()
    {
        var (world, sku) = await SeedAsync();
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 4m, BranchToday.AddDays(200));
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 6m, BranchToday.AddDays(40));

        var items = (await ReadAsync(await world.Vet.GetAsync("/api/staff/dispensary/items"))).GetProperty("items");
        var listed = Assert.Single(items.EnumerateArray(), i => i.GetProperty("skuId").GetGuid() == sku.SkuId);
        Assert.Equal("10", listed.GetProperty("availableQuantity").GetString());
        Assert.Equal("each", listed.GetProperty("stockUnit").GetString());
        Assert.False(listed.GetProperty("isControlledDrug").GetBoolean());

        var batches = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/dispensary/items/{sku.SkuId}/batches"))).EnumerateArray().ToList();
        Assert.Equal([early, late], batches.Select(b => b.GetProperty("batchId").GetGuid()).ToList());
        Assert.Equal([true, false], batches.Select(b => b.GetProperty("isFefoDefault").GetBoolean()).ToList());
        Assert.Equal("6", batches[0].GetProperty("availableQuantity").GetString());
    }

    /// <summary>Whole units (OQ-19): a fractional quantity of a whole-unit SKU is refused outright, never recorded as a shortfall.</summary>
    [Fact]
    public async Task AFractionalQuantity_IsRefused_AsWholeUnitsOnly()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(100));

        var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 0.5m, acceptShortfall: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BusinessViolationCodes.FractionalQuantityNotAllowed, CodeOf(await ReadAsync(response)));
        Assert.Empty(Movements(await LedgerAsync(sku.SkuId)));
    }

    /// <summary>A procedure takes no stock and a billable one is priced at the service price taken now.</summary>
    [Fact]
    public async Task AProcedure_TakesNoStock_AndSnapshotsTheServicePrice()
    {
        var (world, _) = await SeedAsync();

        var response = await RecordAsync(world.Vet, world.VisitId, new { kind = "Procedure", description = "Wound dressing", serviceId = world.Clinic.ServiceId, quantity = 1m, isBillable = true });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var line = Assert.Single((await VisitAsync(world)).GetProperty("treatments").EnumerateArray());
        Assert.Equal("Procedure", line.GetProperty("kind").GetString());
        Assert.True(line.GetProperty("isBillable").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, line.GetProperty("unitPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, line.GetProperty("batch").ValueKind);
    }

    /// <summary>The treatment shape is validated before anything runs: a bad kind, no description for a procedure, a non-positive quantity.</summary>
    [Fact]
    public async Task ABadTreatmentShape_IsA400()
    {
        var (world, sku) = await SeedAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await RecordAsync(world.Vet, world.VisitId, new { kind = "Magic", skuId = sku.SkuId, quantity = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordAsync(world.Vet, world.VisitId, new { kind = "Procedure", quantity = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 0m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordAsync(world.Vet, world.VisitId, new { kind = "Consumable", quantity = 1m })).StatusCode);
    }
// ── owner decision C: auto-split across batches ────────────────────────────────────────────

    /// <summary>One request that needs more than the FEFO batch holds draws on the next batch: one line per batch used, one ledger entry per batch, no
    /// override flag (the split is the FEFO plan), and the warning figure is the whole dispensary's.</summary>
    [Fact]
    public async Task ARequestLargerThanTheFefoBatch_IsSplitAcrossBatches_OneLinePerBatch()
    {
        var (world, sku) = await SeedAsync();
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(40));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200));

        var warned = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 20m));
        Assert.Equal(HttpStatusCode.BadRequest, warned.StatusCode);
        Assert.Equal("13", (await ReadAsync(warned)).GetProperty("availableQuantity").GetString());

        var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 5m));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var body = await ReadAsync(response);
        Assert.Equal("5", body.GetProperty("deductedQuantity").GetString());
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal([early, late], lines.Select(l => l.GetProperty("batch").GetProperty("id").GetGuid()).ToList());
        Assert.Equal(["3", "2"], lines.Select(l => l.GetProperty("deductedQuantity").GetString()).ToList());

        var movements = Movements(await LedgerAsync(sku.SkuId));
        Assert.Equal(2, movements.Count);
        Assert.Equal(-3m, movements.Single(m => m.BatchId == early).Delta);
        Assert.Equal(-2m, movements.Single(m => m.BatchId == late).Delta);
        Assert.All(movements, m => Assert.False(m.FefoOverridden));
        Assert.Equal(0m, await OnHandAsync(early, world.DispensaryId) );
        Assert.Equal(8m, await OnHandAsync(late, world.DispensaryId));

        var visitLines = (await VisitAsync(world)).GetProperty("treatments").EnumerateArray().ToList();
        Assert.Equal(2, visitLines.Count);
        Assert.Equal(0, await AlertCountAsync(sku.SkuId));
    }

    /// <summary>acceptShortfall over the whole dispensary: everything available across batches is deducted, the remainder is the shortfall.</summary>
    [Fact]
    public async Task AcceptShortfall_DeductsEverythingAcrossBatches_AndRecordsOnlyTheRemainder()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(40));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 4m, BranchToday.AddDays(200));

        var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 10m, acceptShortfall: true));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var body = await ReadAsync(response);
        Assert.Equal("7", body.GetProperty("deductedQuantity").GetString());
        Assert.Equal("3", body.GetProperty("shortfallQuantity").GetString());
        Assert.Equal(2, Movements(await LedgerAsync(sku.SkuId)).Count);
        Assert.Equal(3m, await ScalarAsync<decimal>(
            "SELECT quantity_at_raise FROM inventory.stock_alert WHERE sku_id = @s AND kind = @k", ("s", sku.SkuId), ("k", StockAlertKind.Shortfall.Value)));
    }

    /// <summary>A named non-FEFO batch (with a reason) is drawn first; the draw then continues in FEFO order skipping it.</summary>
    [Fact]
    public async Task ANamedNonFefoBatch_IsDrawnFirst_ThenTheDrawContinuesInFefoOrder()
    {
        var (world, sku) = await SeedAsync();
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(40));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 4m, BranchToday.AddDays(200));

        var response = await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 6m, batchId: late, overrideReason: "Keep the early lots for the surgery list"));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var movements = Movements(await LedgerAsync(sku.SkuId));
        // The ledger sorts its rows by batch id, so compare per batch, not by position.
        Assert.Equal(2, movements.Count);
        Assert.Equal(-4m, movements.Single(m => m.BatchId == late).Delta);
        Assert.Equal(-2m, movements.Single(m => m.BatchId == early).Delta);
        Assert.All(movements, m => Assert.True(m.FefoOverridden));
    }

    // ── idempotency ────────────────────────────────────────────────────────────────────────────

    /// <summary>A retry with the same client line id (a timeout, a double click) returns the lines already recorded (200) and deducts nothing again.</summary>
    [Fact]
    public async Task ARetryWithTheSameLineId_IsAnIdempotentReplay_NotASecondDeduction()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(40));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200));
        var lineId = Guid.NewGuid();
        var body = new { lineId, kind = "Consumable", skuId = sku.SkuId, quantity = 5m };

        var first = await RecordAsync(world.Vet, world.VisitId, body);
        var retry = await RecordAsync(world.Vet, world.VisitId, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var firstBody = await ReadAsync(first);
        var retryBody = await ReadAsync(retry);
        Assert.Equal(lineId, firstBody.GetProperty("lineId").GetGuid());
        Assert.Equal(lineId, retryBody.GetProperty("lineId").GetGuid());
        Assert.Equal(firstBody.GetProperty("lines").GetArrayLength(), retryBody.GetProperty("lines").GetArrayLength());
        Assert.Equal(2, Movements(await LedgerAsync(sku.SkuId)).Count);
        Assert.Equal(2, (await VisitAsync(world)).GetProperty("treatments").GetArrayLength());
    }

    // ── concurrency: two reversals of one line ─────────────────────────────────────────────────

    /// <summary>Two reversals of the same line at once: exactly one wins (204), the other is refused (400 already reversed, or 409 on the visit's xmin),
    /// and the stock comes back once.</summary>
    [Fact]
    public async Task TwoConcurrentReversalsOfOneLine_ReturnTheStockOnce()
    {
        var (world, sku) = await SeedAsync();
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var lineId = (await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 4m)))).GetProperty("lineId").GetGuid();

        var results = await Task.WhenAll(
            world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "First" }, Json),
            world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "Second" }, Json));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        var loser = Assert.Single(results, r => r.StatusCode != HttpStatusCode.NoContent);
        Assert.True(loser.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)loser.StatusCode}");
        Assert.Equal(1, Movements(await LedgerAsync(sku.SkuId)).Count(m => m.Kind == MovementKind.Return.Value));
        Assert.Equal(4m, await OnHandAsync(batch, world.QuarantineId));
    }

    // ── paging and bounds ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheDispensaryItemList_IsPaged()
    {
        var (world, first) = await SeedAsync();
        var second = await CreateMedicineSkuAsync(world.Admin, price: 50m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        await ReceiveAsync(world.Admin, first.SkuId, world.DispensaryId, 2m, BranchToday.AddDays(100));
        await ReceiveAsync(world.Admin, second.SkuId, world.DispensaryId, 2m, BranchToday.AddDays(100));

        var page1 = await ReadAsync(await world.Vet.GetAsync("/api/staff/dispensary/items?pageNumber=1&pageSize=1"));
        var page2 = await ReadAsync(await world.Vet.GetAsync("/api/staff/dispensary/items?pageNumber=2&pageSize=1"));

        Assert.Equal(2, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());
        Assert.NotEqual(page1.GetProperty("items")[0].GetProperty("skuId").GetGuid(), page2.GetProperty("items")[0].GetProperty("skuId").GetGuid());
    }

    /// <summary>A procedure quantity beyond numeric(12,3) is a 400, not a 500 at save (SEC 48).</summary>
    [Fact]
    public async Task AProcedureQuantityBeyondTheColumn_IsA400()
    {
        var (world, _) = await SeedAsync();

        var response = await RecordAsync(world.Vet, world.VisitId, new { kind = "Procedure", description = "Dressing", quantity = 1_000_000_000_000m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
/// <summary>A client line id already used on ANOTHER visit is a 400 (TreatmentLineAlreadyRecorded), not a 500 from the primary key.</summary>
    [Fact]
    public async Task ALineIdUsedOnAnotherVisit_Is400_NotA500()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var secondVisit = await OpenAnotherVisitAsync(world);
        var lineId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Created, (await RecordAsync(world.Vet, world.VisitId, new { lineId, kind = "Consumable", skuId = sku.SkuId, quantity = 1m })).StatusCode);

        var response = await RecordAsync(world.Vet, secondVisit, new { lineId, kind = "Consumable", skuId = sku.SkuId, quantity = 1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BusinessViolationCodes.TreatmentLineAlreadyRecorded, CodeOf(await ReadAsync(response)));
        Assert.Single(Movements(await LedgerAsync(sku.SkuId)));
    }

    /// <summary>Reversing the SECOND line of a split returns that line batch and quantity (the ledger movement of a split references the first line).</summary>
    [Fact]
    public async Task ReversingTheSecondLineOfASplit_ReturnsThatLinesBatchAndQuantity()
    {
        var (world, sku) = await SeedAsync();
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(40));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(200));
        var recorded = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 5m)));
        var second = recorded.GetProperty("lines")[1].GetProperty("lineId").GetGuid();

        var reversed = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{second}/reverse", new { reason = "Second batch only" }, Json);

        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);
        var back = Assert.Single(Movements(await LedgerAsync(sku.SkuId)), m => m.Kind == MovementKind.Return.Value);
        Assert.Equal(late, back.BatchId);
        Assert.Equal(2m, back.Delta);
        Assert.Equal(second, back.ReferenceLineId);
        Assert.Equal(2m, await OnHandAsync(late, world.QuarantineId));
        Assert.Equal(0m, await OnHandAsync(early, world.QuarantineId));
        // Lines of one request share an instant, so the visit lists them in id order; look each up by id.
        var statuses = (await VisitAsync(world)).GetProperty("treatments").EnumerateArray().ToDictionary(l => l.GetProperty("id").GetGuid(), l => l.GetProperty("status").GetString());
        Assert.Equal("Reversed", statuses[second]);
        Assert.Equal("Recorded", statuses[recorded.GetProperty("lines")[0].GetProperty("lineId").GetGuid()]);
    }
    private async Task<List<Guid>> VisitLineIdsAsync(PharmacyWorld world) =>
        (await VisitAsync(world)).GetProperty("treatments").EnumerateArray().Select(l => l.GetProperty("id").GetGuid()).ToList();

    private static List<Guid> LineIds(JsonElement recorded) => recorded.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("lineId").GetGuid()).ToList();

    /// <summary>A split of three batches reads back in DRAW order (the lines of one request share an instant, so the visit orders them by position).
    /// Two requests are made so the order is also right across requests.</summary>
    [Fact]
    public async Task ASplitOfThreeBatches_ReadsBackInDrawOrder()
    {
        var (world, sku) = await SeedAsync();
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 2m, BranchToday.AddDays(40));
        var middle = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(100));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(200));

        var first = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Medicine", skuId = sku.SkuId, quantity = 7m, isBillable = true }));
        Assert.Equal([early, middle, late], first.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("batch").GetProperty("id").GetGuid()).ToList());
        world.Clinic.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Medicine", skuId = sku.SkuId, quantity = 2m, isBillable = true }));

        Assert.Equal(LineIds(first).Concat(LineIds(second)).ToList(), await VisitLineIdsAsync(world));
    }

    /// <summary>A named non-FEFO batch is drawn first, then FEFO order over the rest: that is the order the lines read back in.</summary>
    [Fact]
    public async Task ANamedNonFefoBatchFirst_ReadsBackInDrawOrder()
    {
        var (world, sku) = await SeedAsync();
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 2m, BranchToday.AddDays(40));
        var middle = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(100));
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 3m, BranchToday.AddDays(200));

        var recorded = await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new
        {
            kind = "Medicine", skuId = sku.SkuId, quantity = 7m, batchId = late, batchOverrideReason = "Keep the early lots", isBillable = true
        }));

        Assert.Equal([late, early, middle], recorded.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("batch").GetProperty("id").GetGuid()).ToList());
        Assert.Equal(LineIds(recorded), await VisitLineIdsAsync(world));
    }

    // ── TEST gate (2026-10-13): rows TG-1 .. TG-5 of docs/test-cases/TC-HO-EMR-001.md ──────────────────

    private async Task<(PharmacyWorld World, Guid FinalisedVisit, Guid LineId)> FinalisedVisitWithALineAsync()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var lineId = (await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 2m)))).GetProperty("lineId").GetGuid();
        await FinaliseAsync(world);
        return (world, world.VisitId, lineId);
    }

    /// <summary>TG-1 -- the third immutability layer on the treatment table itself: a finalised visit's lines refuse UPDATE, DELETE, INSERT, re-parenting and TRUNCATE.</summary>
    [Fact]
    public async Task TG1_TheLinesOfAFinalisedVisit_RefuseUpdateDeleteInsertMoveAndTruncate()
    {
        var (world, visitId, lineId) = await FinalisedVisitWithALineAsync();
        var openVisit = await OpenAnotherVisitAsync(world);

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_treatment set dose = 'changed' where \"Id\" = @l", ("l", lineId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_treatment set status = 2 where \"Id\" = @l", ("l", lineId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_treatment where \"Id\" = @l", ("l", lineId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_treatment set visit_id = @o where \"Id\" = @l", ("o", openVisit), ("l", lineId)));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.visit_treatment"));
        Assert.Equal("CR001", await SqlStateOfAsync(
            "create temp table emrf_copy as select * from clinic.visit_treatment where \"Id\" = @l; update emrf_copy set \"Id\" = gen_random_uuid(); insert into clinic.visit_treatment select * from emrf_copy;",
            ("l", lineId)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_treatment where visit_id = @v and dose is null and status = 1", ("v", visitId)));
    }

    /// <summary>TG-2 -- an OPEN visit's line cannot be deleted or moved, but a legitimate in-place edit (what a reversal does) still works.</summary>
    [Fact]
    public async Task TG2_TheLinesOfAnOpenVisit_AreNeverDeletedOrMoved_ButMayBeUpdatedInPlace()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var lineId = (await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 1m)))).GetProperty("lineId").GetGuid();
        var other = await OpenAnotherVisitAsync(world);

        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_treatment where \"Id\" = @l", ("l", lineId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_treatment set visit_id = @o where \"Id\" = @l", ("o", other), ("l", lineId)));
        Assert.Null(await SqlStateOfAsync("update clinic.visit_treatment set dose = 'in place' where \"Id\" = @l", ("l", lineId)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_treatment where \"Id\" = @l and visit_id = @v and dose = 'in place'", ("l", lineId), ("v", world.VisitId)));
    }

    /// <summary>TG-3 -- backlog 60(b): both reversals are held at the ledger return until BOTH have loaded the line as "Recorded", so the race is certain.
    /// One wins; the other is refused (xmin guard, 409) and rolled back whole; the stock comes back once.</summary>
    [Fact]
    public async Task TG3_TwoReversalsThatReallyOverlap_ReturnTheStockOnce()
    {
        var gate = new DeductGate();
        var (world, sku) = await SeedAsync(configureServices: services =>
            services.AddScoped<IStockLedger>(sp => new GatedLedger(sp.GetRequiredService<EfStockLedger>(), null, gate)));
        var batch = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(100));
        var lineId = (await ReadAsync(await RecordAsync(world.Vet, world.VisitId, Consumable(sku.SkuId, 4m)))).GetProperty("lineId").GetGuid();

        var results = await Task.WhenAll(
            world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "First" }, Json),
            world.Vet.PostAsJsonAsync($"/api/staff/visits/{world.VisitId}/treatments/{lineId}/reverse", new { reason = "Second" }, Json));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        var loser = Assert.Single(results, r => r.StatusCode != HttpStatusCode.NoContent);
        Assert.True(loser.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)loser.StatusCode}");
        Assert.Equal(1, Movements(await LedgerAsync(sku.SkuId)).Count(m => m.Kind == MovementKind.Return.Value));
        Assert.Equal(4m, await OnHandAsync(batch, world.QuarantineId));
        Assert.Equal(6m, await OnHandAsync(batch, world.DispensaryId));
    }

    /// <summary>TG-4 -- after finalisation the charge lines list splits in DRAW order, not GUID order.</summary>
    [Fact]
    public async Task TG4_ChargeLines_ListASplitInDrawOrder()
    {
        var (world, sku) = await SeedAsync();
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 1m, BranchToday.AddDays(40));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 1m, BranchToday.AddDays(100));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 1m, BranchToday.AddDays(150));
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(200));
        var drawn = new List<Guid>();
        foreach (var quantity in new[] { 4m, 3m })
        {
            drawn.AddRange(LineIds(await ReadAsync(await RecordAsync(world.Vet, world.VisitId, new { kind = "Medicine", skuId = sku.SkuId, quantity, isBillable = true }))));
            world.Clinic.Clock.Advance(TimeSpan.FromMinutes(1)); // two requests never share an instant
        }

        await FinaliseAsync(world);

        var charges = await ChargeLinesAsync(world, world.VisitId);
        Assert.NotNull(charges);
        Assert.Equal(drawn, charges!.Lines.Select(l => l.LineId).ToList());
    }

    /// <summary>TG-5 -- ServiceDate near midnight, the other way: opened 17:30 UTC and finalised 17:50 UTC is 23:50 in Dhaka, the SAME date, not the next.</summary>
    [Fact]
    public async Task TG5_AVisitFinalisedBeforeDhakaMidnight_HasThatDaysServiceDate()
    {
        var opened = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(17).AddMinutes(30), TimeSpan.Zero);
        var (world, _) = await SeedAsync(start: opened);
        world.Clinic.Clock.Advance(TimeSpan.FromMinutes(20));

        await FinaliseAsync(world);

        var charges = await ChargeLinesAsync(world, world.VisitId);
        Assert.NotNull(charges);
        Assert.Equal(DateOnly.FromDateTime(opened.UtcDateTime), charges!.ServiceDate);
    }
}
