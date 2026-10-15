using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 §16 phase E -- I-E1 ... I-E10: FEFO, the reservation entries, the last-unit race (with REAL parallelism, §20.4), the expiry refusals
/// everywhere including the database's own, and the published availability contract. The port contract tests resolve <c>IStockLedger</c> from a fresh
/// DI scope, call it, then commit through the scoped <c>CatEcommerceDbContext</c> -- exactly what a consumer handler does.
/// </summary>
public sealed class InventoryStockPortEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static DateOnly InDays(int days) => UtcToday.AddDays(days);

    private sealed record Stocked(BranchContext Branch, SeededSku Sku, Guid Soon, Guid Middle, Guid Late);

    /// <summary>One SKU with three batches: expiring in 30 days (warehouse), 60 (shop floor) and 90 (warehouse), 10 each.</summary>
    private async Task<Stocked> StockThreeBatchesAsync()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var soon = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, InDays(30));
        var middle = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m, InDays(60));
        var late = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, InDays(90));
        return new Stocked(branch, sku, soon, middle, late);
    }

    // ── I-E1 / I-E2 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E1_TheFefoPreviewAllocatesTheSoonestExpiringBatchFirst_SplitAsNeeded()
    {
        var s = await StockThreeBatchesAsync();

        var preview = await JsonOkAsync(await s.Branch.Admin.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=15&purpose=Sale"));

        var lines = preview.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(s.Soon, lines[0].GetProperty("batchId").GetGuid());
        Assert.Equal("10", lines[0].GetProperty("quantity").GetString());
        Assert.Equal(s.Middle, lines[1].GetProperty("batchId").GetGuid());
        Assert.Equal("5", lines[1].GetProperty("quantity").GetString());
        Assert.Equal("0", preview.GetProperty("shortfall").GetString());
        Assert.Equal("each", preview.GetProperty("stockUnit").GetString());
    }

    [Fact]
    public async Task I_E2_APreviewSkipsAnExpiredBatchAndAQuarantinedOne()
    {
        var s = await StockThreeBatchesAsync();
        await SetBatchExpiryDirectlyAsync(s.Soon, InDays(-3));
        Assert.Equal(HttpStatusCode.NoContent, (await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{s.Middle}/quarantine", new { reason = "Torn bags." }, Json)).StatusCode);

        var preview = await JsonOkAsync(await s.Branch.Admin.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=15&purpose=Sale"));

        var line = Assert.Single(preview.GetProperty("lines").EnumerateArray());
        Assert.Equal(s.Late, line.GetProperty("batchId").GetGuid());
        Assert.Equal("10", line.GetProperty("quantity").GetString());
        Assert.Equal("5", preview.GetProperty("shortfall").GetString());
    }

    [Fact]
    public async Task I_E1_APreviewForAPurposeThatLocationKindDoesNotPermitIsA400_AndTheNeededFieldsAreChecked()
    {
        var s = await StockThreeBatchesAsync();

        var dispensary = await s.Branch.Admin.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=1&purpose=Sale&locationId={s.Branch.DispensaryId}");
        Assert.Equal(HttpStatusCode.BadRequest, dispensary.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Branch.Admin.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=1&purpose=Receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Branch.Admin.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=0&purpose=Sale")).StatusCode);
    }

    // ── I-E3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E3_AnExplicitNonFefoReservationNeedsAReason_AndWithOneIsFlaggedAndAuditedInTheSameCommit()
    {
        var s = await StockThreeBatchesAsync();
        var reference = OrderReference();
        await using var port = OpenPort();

        var without = await port.Ledger.ReserveAsync(Demand(s.Branch.BranchId, s.Sku.SkuId, 2m, lines: [(s.Late, s.Branch.WarehouseId, 2m)]), reference, Customer(), default);
        AssertPort(without, BusinessViolationCodes.AllocationOverrideReasonRequired);
        Assert.Equal(0, port.Enlisted);

        var with = await port.Ledger.ReserveAsync(
            Demand(s.Branch.BranchId, s.Sku.SkuId, 2m, lines: [(s.Late, s.Branch.WarehouseId, 2m)], reason: "The customer asked for the freshest bags."), reference, Customer(), default);
        Assert.True(with.IsSuccess, with.Message);
        Assert.True(Assert.Single(with.Value!.Lines).FefoOverridden);
        Assert.Equal(2, port.Enlisted); // the reservation entry and the audit entry, nothing saved yet
        Assert.Equal(0m, (await PositionAsync(s.Late, s.Branch.WarehouseId)).Reserved);

        Assert.Equal("committed", await CommitOutcomeAsync(port));

        Assert.Equal(2m, (await PositionAsync(s.Late, s.Branch.WarehouseId)).Reserved);
        await using var verify = OpenPort();
        var entry = await verify.Db.StockReservationEntries.AsNoTracking().SingleAsync(e => e.ReferenceId == reference.ReferenceId);
        Assert.True(entry.FefoOverridden);
        Assert.Equal("The customer asked for the freshest bags.", entry.OverrideReason);
        Assert.Equal(port.UserId, entry.PerformedBy);

        var audit = await verify.Db.AuditEntries.AsNoTracking().SingleAsync(a => a.EntityId == reference.ReferenceId && a.Action == "FefoOverridden");
        Assert.Equal(AuditCategory.BusinessEvent, audit.Category);
        Assert.Equal(port.UserId, audit.ActorUserId);
        Assert.Equal("The customer asked for the freshest bags.", audit.Reason);
        Assert.Contains(s.Soon.ToString(), audit.BeforeValue!, StringComparison.OrdinalIgnoreCase); // FEFO's own plan
        Assert.Contains(s.Late.ToString(), audit.AfterValue!, StringComparison.OrdinalIgnoreCase);   // the chosen plan
    }

    [Fact]
    public async Task I_E3_AnExplicitPlanThatEqualsFefoIsNotFlaggedAndWritesNoAuditEntry()
    {
        var s = await StockThreeBatchesAsync();
        var reference = OrderReference();
        await using var port = OpenPort();

        var result = await port.Ledger.ReserveAsync(Demand(s.Branch.BranchId, s.Sku.SkuId, 4m, lines: [(s.Soon, s.Branch.WarehouseId, 4m)]), reference, Customer(), default);

        Assert.True(result.IsSuccess, result.Message);
        Assert.False(Assert.Single(result.Value!.Lines).FefoOverridden);
        Assert.Equal(1, port.Enlisted);
        Assert.Equal("committed", await CommitOutcomeAsync(port));
        await using var verify = OpenPort();
        Assert.False(await verify.Db.AuditEntries.AnyAsync(a => a.EntityId == reference.ReferenceId && a.Action == "FefoOverridden"));
    }

    // ── I-E4 / I-E5: the race, with real parallelism ────────────────────────────────────────────────────────────────────────────

    /// <summary>Opens <paramref name="parallelism"/> scopes, has EACH plan and enlist its claim, releases them all at once and commits them
    /// concurrently -- planning happens before any commit, so every scope believes the unit is free and the DATABASE is the only referee (§20.4: no
    /// retries, no delays, no serialisation in the test).</summary>
    private async Task<IReadOnlyList<string>> RaceAsync(int parallelism, Func<Port, Task> claim)
    {
        var ports = new List<Port>();
        try
        {
            for (var i = 0; i < parallelism; i++) ports.Add(OpenPort());
            await Task.WhenAll(ports.Select(claim));
            Assert.All(ports, p => Assert.True(p.Enlisted > 0, "every scope must have a claim to commit"));

            using var gate = new ManualResetEventSlim(false);
            var commits = ports.Select(p => Task.Run(() => { gate.Wait(); return CommitOutcomeAsync(p); })).ToList();
            gate.Set();
            return await Task.WhenAll(commits);
        }
        finally
        {
            foreach (var port in ports) await port.DisposeAsync();
        }
    }

    [Fact]
    public async Task I_E4_TwentyParallelClaimsOnTheLastFreeUnit_ExactlyOneCommitsAndTheOthersFailTheProjectionsCheck()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 1m);

        var outcomes = await RaceAsync(20, async port =>
        {
            var result = await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 1m), OrderReference(), Customer(), default);
            Assert.True(result.IsSuccess, result.Message);
        });

        Assert.Equal(1, outcomes.Count(o => o == "committed"));
        Assert.Equal(19, outcomes.Count(o => o == "23514:ck_stock_position_balance"));
        var position = await PositionAsync(batchId, branch.WarehouseId);
        Assert.Equal(1m, position.Reserved);
        Assert.Equal(1m, position.OnHand);
    }

    [Fact]
    public async Task I_E5_TheSameRaceThroughAPosDeduction_ExactlyOneSaleCommits_AndOnHandIsZero()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 1m);

        var outcomes = await RaceAsync(20, async port =>
        {
            var result = await port.Ledger.DeductAsync(
                Demand(branch.BranchId, sku.SkuId, 1m), new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-1"), new StockParty(null, null), default);
            Assert.True(result.IsSuccess, result.Message);
        });

        Assert.Equal(1, outcomes.Count(o => o == "committed"));
        Assert.Equal(19, outcomes.Count(o => o == "23514:ck_stock_position_balance"));
        Assert.Equal(0m, (await PositionAsync(batchId, branch.ShopId)).OnHand);
        Assert.Equal(2, await LedgerRowCountAsync([batchId])); // the receipt, plus exactly one sale
    }

    [Fact]
    public async Task I_E4_AReservationAndADeductionRacingForTheLastUnit_HaveExactlyOneWinner()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 1m);
        var claims = 0;

        var outcomes = await RaceAsync(10, async port =>
        {
            if (Interlocked.Increment(ref claims) % 2 == 0)
                Assert.True((await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 1m), OrderReference(), Customer(), default)).IsSuccess);
            else
                Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m), new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS"), new StockParty(null, null), default)).IsSuccess);
        });

        Assert.Equal(1, outcomes.Count(o => o == "committed"));
        var position = await PositionAsync(batchId, branch.WarehouseId);
        Assert.True((position.OnHand, position.Reserved) is (0m, 0m) or (1m, 1m), $"on hand {position.OnHand}, reserved {position.Reserved}");
    }

    // ── I-E6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E6_SellableExcludesExpiredQuarantinedHeldAndReservedStock_ViaTheQueryAndTheView()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var good = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m, InDays(45));
        var expired = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 6m, InDays(20));
        var quarantined = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 4m, InDays(80));
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.QuarantineId, 9m, InDays(80));
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 5m, InDays(80));

        await SetBatchExpiryDirectlyAsync(expired, InDays(-2));
        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{quarantined}/quarantine", new { reason = "Torn bags." }, Json)).StatusCode);

        await using (var port = OpenPort())
        {
            var reserved = await port.Ledger.ReserveAsync(
                Demand(branch.BranchId, sku.SkuId, 3m, lines: [(good, branch.ShopId, 3m)]), OrderReference(), Customer(), default);
            Assert.True(reserved.IsSuccess, reserved.Message);
            Assert.Equal("committed", await CommitOutcomeAsync(port));
        }

        await using var query = OpenPort();
        var sellable = await query.Db.SellableStock.AsNoTracking().SingleAsync(v => v.BranchId == branch.BranchId && v.SkuId == sku.SkuId);
        Assert.Equal(7m, sellable.SellableQuantity);
        Assert.Equal(InDays(45), sellable.NextExpiresOn);

        // The contract query HO-SHP-001 and HO-ORD-001 consume (ADR-116): every requested SKU answered, zero when none.
        var dispatcher = Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var result = (await dispatcher.GetResultAsync(new GetSellableStockQuery
        {
            BranchId = branch.BranchId, SkuIds = [sku.SkuId, Guid.NewGuid()],
            UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        })).Result!;
        Assert.Equal(7m, result.Single(r => r.SkuId == sku.SkuId).SellableQuantity);
        Assert.Equal(InDays(45), result.Single(r => r.SkuId == sku.SkuId).NextExpiresOn);
        Assert.Equal(0m, result.Single(r => r.SkuId != sku.SkuId).SellableQuantity);

        // The staff stock read agrees: sellable 7, held 13 (the quarantined batch's 4 + the quarantine location's 9), expired 6.
        var summary = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock?search={sku.SkuCode}"));
        var row = Assert.Single(summary.GetProperty("items").EnumerateArray());
        Assert.Equal("7", row.GetProperty("sellable").GetString());
        Assert.Equal("12", row.GetProperty("usable").GetString()); // 7 at retail + 5 at the dispensary
        Assert.Equal("13", row.GetProperty("heldQuantity").GetString());
        Assert.Equal("6", row.GetProperty("expiredQuantity").GetString());
        Assert.Equal("3", row.GetProperty("reserved").GetString());
    }

    // ── I-E7 / I-E8 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E7_AnExpiredBatchIsRefusedForEveryUse_EvenWithAnExplicitBatchAndAReason_AndNothingIsEnlisted()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        // One batch per purpose, each at a location that permits it, so the only thing in the way of every use is the expiry.
        var warehouse = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, InDays(30));
        var dispensary = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 10m, InDays(30));
        var boarding = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.BoardingId, 10m, InDays(30));
        foreach (var batch in new[] { warehouse, dispensary, boarding }) await SetBatchExpiryDirectlyAsync(batch, InDays(-1));

        var reason = "The vet says it is fine, I am sure of it.";
        var party = Customer(catId: Guid.NewGuid());
        await using var port = OpenPort();

        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Sale, [(warehouse, branch.WarehouseId, 1m)], reason), OrderReference(), party, default), BusinessViolationCodes.BatchExpired);
        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Dispense, [(dispensary, branch.DispensaryId, 1m)], reason), OrderReference(kind: StockReferenceKind.Prescription), party, default), BusinessViolationCodes.BatchExpired);
        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.ClinicalUse, [(dispensary, branch.DispensaryId, 1m)], reason), OrderReference(kind: StockReferenceKind.Visit), party, default), BusinessViolationCodes.BatchExpired);
        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.BoardingUse, [(boarding, branch.BoardingId, 1m)], reason), OrderReference(kind: StockReferenceKind.BoardingStay), party, default), BusinessViolationCodes.BatchExpired);
        AssertPort(await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Sale, [(warehouse, branch.WarehouseId, 1m)], reason), OrderReference(), party, default), BusinessViolationCodes.BatchExpired);

        Assert.Equal(0, port.Enlisted);
        // A plan with no explicit batch cannot choose it either -- FEFO skips it, so the demand is simply unmet.
        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m), OrderReference(), party, default), BusinessViolationCodes.InsufficientStock);
        Assert.Equal(0, port.Enlisted);
    }

    [Fact]
    public async Task I_E8_DefenceInDepth_ASaleRowForAnExpiredBatchInsertedDirectlyIsRefusedByTheDatabase_AndThePositionIsUnchanged()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(10));
        await SetBatchExpiryDirectlyAsync(batchId, InDays(-1));

        var refused = await InsertLedgerRowDirectlyAsync(branch.BranchId, branch.WarehouseId, sku.SkuId, batchId, MovementKind.Sale, -1m);

        Assert.Equal("23514", refused.SqlState);
        Assert.Equal("tg_stock_batch_expired", refused.ConstraintName);
        Assert.Equal(5m, (await PositionAsync(batchId, branch.WarehouseId)).OnHand);
        Assert.Equal(1, await LedgerRowCountAsync([batchId]));
    }

    [Fact]
    public async Task I_E8_TheSameTriggerRefusesAQuarantinedBatch_AndStillAllowsAnInventoryMovementOfIt()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m);
        await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/quarantine", new { reason = "Torn bags." }, Json);

        var refused = await InsertLedgerRowDirectlyAsync(branch.BranchId, branch.WarehouseId, sku.SkuId, batchId, MovementKind.Sale, -1m);
        Assert.Equal("tg_stock_batch_not_usable", refused.ConstraintName);

        // BR-3 forbids USE, not movement: a quarantined batch can still be written off.
        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/write-off", new { reason = "Disposal." }, Json)).StatusCode);
    }

    // ── I-E9 / I-E10 ────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E9_ReserveThenConsume_WritesASaleAndFreesTheReservation_AndReserveThenRelease_RestoresFreeStock()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m);
        var order = OrderReference();
        var customer = Customer();

        await using (var reserve = OpenPort())
        {
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 3m), order, customer, default)).IsSuccess);
            Assert.Equal("committed", await CommitOutcomeAsync(reserve));
        }
        Assert.Equal((10m, 3m), await PositionAsync(batchId, branch.WarehouseId));

        await using (var consume = OpenPort())
        {
            var consumed = await consume.Ledger.ConsumeReservationAsync(order, customer, default);
            Assert.True(consumed.IsSuccess, consumed.Message);
            var line = Assert.Single(consumed.Value!);
            Assert.Equal(batchId, line.BatchId);
            Assert.Equal(3m, line.Quantity);
            Assert.Equal("committed", await CommitOutcomeAsync(consume));
        }
        Assert.Equal((7m, 0m), await PositionAsync(batchId, branch.WarehouseId));

        await using (var again = OpenPort())
            AssertPort(await again.Ledger.ConsumeReservationAsync(order, customer, default), BusinessViolationCodes.ReservationNotFound);

        var second = OrderReference();
        await using (var reserve = OpenPort())
        {
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 2m), second, customer, default)).IsSuccess);
            await reserve.CommitAsync();
        }
        Assert.Equal((7m, 2m), await PositionAsync(batchId, branch.WarehouseId));

        await using (var release = OpenPort())
        {
            Assert.True((await release.Ledger.ReleaseAsync(second, default)).IsSuccess);
            await release.CommitAsync();
        }
        Assert.Equal((7m, 0m), await PositionAsync(batchId, branch.WarehouseId));

        await using var idempotent = OpenPort();
        Assert.True((await idempotent.Ledger.ReleaseAsync(second, default)).IsSuccess);
        Assert.Equal(0, idempotent.Enlisted);

        var ledger = await JsonOkAsync(await branch.Admin.Client.GetAsync(
            $"/api/staff/inventory/ledger?from={InDays(-1):yyyy-MM-dd}&to={InDays(1):yyyy-MM-dd}&batchId={batchId}&kind=Sale"));
        var sale = Assert.Single(ledger.GetProperty("items").EnumerateArray());
        Assert.Equal("-3", sale.GetProperty("quantityDelta").GetString());
        Assert.Equal("CustomerOrder", sale.GetProperty("referenceKind").GetString());
    }

    [Fact]
    public async Task I_E10_AnUnreservedDeductionCannotEatAnotherCustomersReservation()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);

        await using (var reserve = OpenPort())
        {
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 8m), OrderReference(), Customer(), default)).IsSuccess);
            await reserve.CommitAsync();
        }

        await using var port = OpenPort();
        var result = await port.Ledger.DeductAsync(
            Demand(branch.BranchId, sku.SkuId, 3m), new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-2"), new StockParty(null, null), default);

        AssertPort(result, BusinessViolationCodes.InsufficientStock);
        Assert.Contains("2", result.Message);
        Assert.Equal(0, port.Enlisted);
        Assert.Equal((10m, 8m), await PositionAsync(batchId, branch.ShopId));
    }

    // ── the party stamp, returns, corrections and balances ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E10_ASaleNeedsTheCustomerUnlessItIsACounterSale_AndADispenseNeedsTheCat()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 5m);
        await using var port = OpenPort();

        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m), OrderReference(), new StockParty(null, null), default), BusinessViolationCodes.StockPartyRequired);
        AssertPort(await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Dispense), OrderReference(kind: StockReferenceKind.Prescription), new StockParty(Guid.NewGuid(), null), default), BusinessViolationCodes.StockPartyRequired);
        Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m), new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS"), new StockParty(null, null), default)).IsSuccess);
        Assert.Equal(1, port.Enlisted);
    }

    [Fact]
    public async Task Returns_GoIntoTheBranchQuarantineLocationOnly_ForABatchThePartyReceived()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m);
        var buyer = Customer();
        var order = OrderReference();

        await using (var sale = OpenPort())
        {
            Assert.True((await sale.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 2m), order, buyer, default)).IsSuccess);
            await sale.CommitAsync();
        }

        await using (var stranger = OpenPort())
            AssertPort(await stranger.Ledger.ReceiveReturnAsync(new StockReturn(branch.BranchId, sku.SkuId, batchId, 1m, "Opened"), OrderReference(kind: StockReferenceKind.CustomerReturn), Customer(), default), BusinessViolationCodes.ReturnNotTraceable);

        await using var port = OpenPort();
        var returned = await port.Ledger.ReceiveReturnAsync(new StockReturn(branch.BranchId, sku.SkuId, batchId, 1m, "Opened"), OrderReference(kind: StockReferenceKind.CustomerReturn), buyer, default);
        Assert.True(returned.IsSuccess, returned.Message);
        Assert.Equal(branch.QuarantineId, Assert.Single(returned.Value!).LocationId);
        await port.CommitAsync();

        Assert.Equal(1m, (await PositionAsync(batchId, branch.QuarantineId)).OnHand);
        Assert.Equal(3m, (await PositionAsync(batchId, branch.ShopId)).OnHand);
    }

    [Fact]
    public async Task Balances_AreReportedPerBatchPerLocation_WithReservedSeparate()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var a = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m, lot: "BAL-A");
        await using (var reserve = OpenPort())
        {
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 2m), OrderReference(), Customer(), default)).IsSuccess);
            await reserve.CommitAsync();
        }

        await using var port = OpenPort();
        var balances = await port.Ledger.GetBranchBalancesAsync(branch.BranchId, [sku.SkuId], default);

        var balance = Assert.Single(balances.Value!);
        Assert.Equal((a, branch.ShopId, "BAL-A", 5m, 2m), (balance.BatchId, balance.LocationId, balance.LotNumber, balance.OnHand, balance.Reserved));
        Assert.Equal(0, port.Enlisted);
    }

    // ── members added 2026-10-09 (HO-ORD-001 architecture-gate fix, owner decision 2): ReserveBatchAsync and GetBatchUsabilityAsync ──

    [Fact]
    public async Task ReserveBatch_TheCallerNamesTheBatchAndTheReason_InventoryPicksTheLocations_AndTheOverrideIsFlaggedAndAudited()
    {
        var s = await StockThreeBatchesAsync();
        var reference = OrderReference();
        await using var port = OpenPort();

        var plan = await port.Ledger.ReserveBatchAsync(new StockBatchDemand(s.Branch.BranchId, s.Sku.SkuId, 4m, s.Late, "The customer asked for the freshest bags."), reference, Customer(), default);

        Assert.True(plan.IsSuccess, plan.Message);
        var line = Assert.Single(plan.Value!.Lines);
        Assert.Equal((s.Late, s.Branch.WarehouseId, 4m, true), (line.BatchId, line.LocationId, line.Quantity, line.FefoOverridden));   // the only location holding that batch
        Assert.Equal("committed", await CommitOutcomeAsync(port));
        Assert.Equal(4m, (await PositionAsync(s.Late, s.Branch.WarehouseId)).Reserved);

        await using var verify = OpenPort();
        Assert.True(await verify.Db.AuditEntries.AsNoTracking().AnyAsync(a => a.EntityId == reference.ReferenceId && a.Action == "FefoOverridden"));
    }

    [Fact]
    public async Task ReserveBatch_RefusesAnUnknownBatch_AnotherBranchsBatch_AnExpiredOne_AndAShortOne_AndEnlistsNothing()
    {
        var s = await StockThreeBatchesAsync();
        var other = await StockThreeBatchesAsync();
        await SetBatchExpiryDirectlyAsync(s.Soon, InDays(-3));
        await using var port = OpenPort();

        AssertPort(await port.Ledger.ReserveBatchAsync(new StockBatchDemand(s.Branch.BranchId, s.Sku.SkuId, 1m, Guid.NewGuid(), "A good reason"), OrderReference(), Customer(), default), BusinessViolationCodes.BatchNotFound);
        AssertPort(await port.Ledger.ReserveBatchAsync(new StockBatchDemand(s.Branch.BranchId, s.Sku.SkuId, 1m, other.Late, "A good reason"), OrderReference(), Customer(), default), BusinessViolationCodes.BatchNotFound);
        AssertPort(await port.Ledger.ReserveBatchAsync(new StockBatchDemand(s.Branch.BranchId, s.Sku.SkuId, 1m, s.Soon, "A good reason"), OrderReference(), Customer(), default), BusinessViolationCodes.BatchExpired);
        AssertPort(await port.Ledger.ReserveBatchAsync(new StockBatchDemand(s.Branch.BranchId, s.Sku.SkuId, 11m, s.Late, "A good reason"), OrderReference(), Customer(), default), BusinessViolationCodes.InsufficientStock);
        Assert.Equal(0, port.Enlisted);
    }

    [Fact]
    public async Task BatchUsability_SaysRecalledExpiredWithdrawnOrUnknown_ForTheBranchsLocalDate_AndWritesNothing()
    {
        var s = await StockThreeBatchesAsync();
        await SetBatchExpiryDirectlyAsync(s.Soon, InDays(-3));
        Assert.Equal(HttpStatusCode.NoContent, (await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{s.Middle}/quarantine", new { reason = "Torn bags." }, Json)).StatusCode);
        var unknown = Guid.NewGuid();
        await using var port = OpenPort();

        var result = await port.Ledger.GetBatchUsabilityAsync(s.Branch.BranchId, [s.Soon, s.Middle, s.Late, unknown], default);

        Assert.True(result.IsSuccess);
        var byBatch = result.Value!.ToDictionary(u => u.BatchId);
        Assert.Equal(BusinessViolationCodes.BatchExpired, byBatch[s.Soon].RefusalCode);
        Assert.Equal(BusinessViolationCodes.BatchNotUsable, byBatch[s.Middle].RefusalCode);
        Assert.True(byBatch[s.Late].IsUsable);
        Assert.Equal(s.Sku.SkuId, byBatch[s.Late].SkuId);
        Assert.Equal(BusinessViolationCodes.BatchNotFound, byBatch[unknown].RefusalCode);
        Assert.Equal(0, port.Enlisted);

        var elsewhere = await StockThreeBatchesAsync();
        var crossBranch = await port.Ledger.GetBatchUsabilityAsync(elsewhere.Branch.BranchId, [s.Late], default);
        Assert.Equal(BusinessViolationCodes.BatchNotFound, Assert.Single(crossBranch.Value!).RefusalCode);   // a batch of another branch does not exist for this one
    }

    [Fact]
    public async Task BatchUsability_ARecalledBatch_IsReportedAsRecalled()
    {
        var s = await StockThreeBatchesAsync();
        string lot;
        await using (var lookup = OpenPort()) lot = await lookup.Db.Batches.AsNoTracking().Where(b => b.Id == s.Late).Select(b => b.LotNumber).SingleAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{s.Late}/recall", new { confirmLotNumber = lot, reason = "Supplier notice.", customerAdvice = "Stop feeding it." }, Json)).StatusCode);
        await using var port = OpenPort();

        var result = await port.Ledger.GetBatchUsabilityAsync(s.Branch.BranchId, [s.Late], default);

        var usability = Assert.Single(result.Value!);
        Assert.True(usability.IsRecalled);
        Assert.Equal(BusinessViolationCodes.BatchRecalled, usability.RefusalCode);
    }

    [Fact]
    public async Task DispensaryBatches_AreOnlyThoseHeldAtTheClinicDispensary_WithTheirBatchStatus_AndTheDispensaryLocationIsPublished()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var otherSku = await CreateSkuAsync(branch.Admin.Client);
        var inDispensary = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 4m, lot: "DSP-A");
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 9m, lot: "DSP-SHOP");
        await ReceiveAsync(branch.Admin.Client, otherSku.SkuId, branch.DispensaryId, 3m, lot: "DSP-OTHER");

        await using var port = OpenPort();
        var batches = await port.Ledger.GetDispensaryBatchesAsync(branch.BranchId, [sku.SkuId], default);

        var only = Assert.Single(batches.Value!);
        Assert.Equal((sku.SkuId, inDispensary, "DSP-A", branch.DispensaryId, 4m, 0m, BatchStatus.Available), (only.SkuId, only.BatchId, only.LotNumber, only.LocationId, only.OnHand, only.Reserved, only.BatchStatus));
        Assert.Equal(4m, only.Free);
        Assert.Empty((await port.Ledger.GetDispensaryBatchesAsync(branch.BranchId, [], default)).Value!);

        var location = await port.Ledger.GetClinicDispensaryAsync(branch.BranchId, default);
        Assert.Equal(branch.DispensaryId, location.Value!.LocationId);

        var missing = await port.Ledger.GetClinicDispensaryAsync(Guid.NewGuid(), default);
        Assert.False(missing.IsSuccess);
        Assert.Equal("DispensaryNotFound", missing.ViolationCode);
        Assert.Equal(0, port.Enlisted);
    }

    [Fact]
    public async Task Correction_AdjustAsyncMovesStockOnce_NeedsAReason_AndWritesOneBusinessEventAuditEntryNamingTheReference()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 10m);
        var reference = new StockReference(StockReferenceKind.ControlledDrugCorrection, Guid.NewGuid(), null, "CD-CORR-1");
        await using var port = OpenPort();

        AssertPort(await port.Ledger.AdjustAsync(new StockAdjustment(branch.BranchId, sku.SkuId, batchId, branch.DispensaryId, -1m, "no", null), reference, new StockParty(null, null), default),
            BusinessViolationCodes.AdjustmentNoteRequired);

        var done = await port.Ledger.AdjustAsync(new StockAdjustment(branch.BranchId, sku.SkuId, batchId, branch.DispensaryId, -1m, "Register counted one fewer ampoule.", null), reference, new StockParty(null, null), default);
        Assert.True(done.IsSuccess, done.Message);
        Assert.Equal(2, port.Enlisted);
        await port.CommitAsync();

        Assert.Equal(9m, (await PositionAsync(batchId, branch.DispensaryId)).OnHand);
        await using var verify = OpenPort();
        var audit = await verify.Db.AuditEntries.AsNoTracking().SingleAsync(a => a.EntityId == reference.ReferenceId && a.Action == "StockCorrectedThroughLedger");
        Assert.Equal(AuditCategory.BusinessEvent, audit.Category);
        Assert.Equal("Register counted one fewer ampoule.", audit.Reason);
    }
}
