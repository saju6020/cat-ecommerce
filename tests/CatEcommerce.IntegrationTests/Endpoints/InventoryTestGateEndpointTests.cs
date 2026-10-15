using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Api.Background;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 TEST gate additions (docs/test-cases/TC-HO-IMP-001.md): the holes the earlier gates named. Reconciliation against real drift (U-J2), a recall that
/// reaches a batch held in a SECOND branch (I-I1), the write-off assignment refusal, the real ledger trigger's two-statement body under two real sessions (FI-1),
/// a recall racing SEVERAL reservations (I-I7), the alert-raise race with a fresh key every round, and the database's refusal to go negative. Every row these tests
/// touch belongs to a branch the test created; the only direct writes are the drift (done and undone inside a transaction that disables the position guard and
/// re-enables it before it commits) and ledger rows that the database must REFUSE or that belong to the test's own batch.
/// </summary>
public sealed class InventoryTestGateEndpointTests(InventoryApiFactory factory, ITestOutputHelper output) : InventoryIntegrationTestBase(factory)
{
    private static DateOnly InDays(int days) => UtcToday.AddDays(days);

    // ── U-J2: reconciliation with a drifted position ────────────────────────────────────────────────────────────────────────────

    /// <summary>Moves one test-owned position's <c>on_hand</c> by <paramref name="by"/> WITHOUT a ledger entry: the position guard is disabled inside this transaction
    /// and enabled again before it commits, so the trigger is never left off for anyone else.</summary>
    private static async Task DriftPositionAsync(Guid batchId, Guid locationId, decimal by)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using (var disable = new NpgsqlCommand("ALTER TABLE inventory.stock_position DISABLE TRIGGER tg_stock_position_guard", connection, tx)) await disable.ExecuteNonQueryAsync();
        await using (var update = new NpgsqlCommand("UPDATE inventory.stock_position SET on_hand = on_hand + @by WHERE batch_id = @b AND location_id = @l", connection, tx))
        {
            update.Parameters.AddWithValue("by", by);
            update.Parameters.AddWithValue("b", batchId);
            update.Parameters.AddWithValue("l", locationId);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        await using (var enable = new NpgsqlCommand("ALTER TABLE inventory.stock_position ENABLE TRIGGER tg_stock_position_guard", connection, tx)) await enable.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private static async Task<string> PositionGuardStateAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT tgenabled::text FROM pg_trigger WHERE tgname = 'tg_stock_position_guard' AND tgrelid = 'inventory.stock_position'::regclass", connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task U_J2_ReconciliationReportsAPositionDriftedFromItsLedger_RaisesALedgerMismatchAlert_AndIsEmptyOnceRepaired()
    {
        var branch = await SeedBranchAsync();
        var drifted = await CreateSkuAsync(branch.Admin.Client);
        var honest = await CreateSkuAsync(branch.Admin.Client);
        var driftedBatch = await ReceiveAsync(branch.Admin.Client, drifted.SkuId, branch.ShopId, 10m);
        await ReceiveAsync(branch.Admin.Client, honest.SkuId, branch.ShopId, 7m);
        Assert.Equal(0, (await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/reconciliation"))).GetProperty("items").GetArrayLength());

        await DriftPositionAsync(driftedBatch, branch.ShopId, 5m);
        try
        {
            Assert.Equal("O", await PositionGuardStateAsync()); // the guard is back on for everyone
            var report = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/reconciliation"));
            var row = Assert.Single(report.GetProperty("items").EnumerateArray()); // the honest position is not reported
            Assert.Equal(driftedBatch, row.GetProperty("batchId").GetGuid());
            Assert.Equal("15", row.GetProperty("onHand").GetString());
            Assert.Equal("10", row.GetProperty("expectedOnHand").GetString());

            using (var scope = Factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<InventoryWorkCycle>().RunReconciliationPassAsync();

            var alerts = (await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/alerts?pageSize=100&kind=LedgerMismatch"))).GetProperty("items").EnumerateArray().ToList();
            var alert = Assert.Single(alerts);
            Assert.Equal(drifted.SkuId, alert.GetProperty("skuId").GetGuid());
            Assert.Equal(driftedBatch, alert.GetProperty("batchId").GetGuid());
        }
        finally
        {
            await DriftPositionAsync(driftedBatch, branch.ShopId, -5m);
        }

        Assert.Equal(0, (await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/reconciliation"))).GetProperty("items").GetArrayLength());
        Assert.Equal("O", await PositionGuardStateAsync());
    }

    // ── a batch held in TWO branches ─────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record TwoBranches(
        BranchContext First, StaffClient Keeper, HttpClient AdminInSecond, Guid SecondBranchId, Guid SecondWarehouseId, SeededSku Sku, Guid BatchId, string Lot);

    /// <summary>One batch of 10 at the first branch's warehouse, 4 moved to the shop floor and 3 moved to a warehouse of a SECOND branch (the administrator is
    /// assigned to both; a transfer may cross branches when the caller is assigned to both). The keeper is assigned to the first branch only.</summary>
    private async Task<TwoBranches> HoldInTwoBranchesAsync()
    {
        var first = await SeedBranchAsync();
        var keeper = await StaffAsync(first, StaffRoles.Inventory);

        var code = NewCode("B");
        var created = await first.Admin.Client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Second Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        var secondId = (await JsonOkAsync(created)).GetProperty("branchId").GetGuid();
        var assign = await first.Admin.Client.PutAsJsonAsync($"/api/admin/staff/{first.Admin.StaffAccountId}/branches", new { branchIds = new[] { first.BranchId, secondId }, primaryBranchId = first.BranchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var inSecond = await SignInStaffOnAsync(Factory, first.Admin.Mobile, first.Admin.Password);
        UseBranch(inSecond, secondId);
        var secondWarehouse = await CreateLocationAsync(inSecond, "Warehouse");

        var sku = await CreateSkuAsync(first.Admin.Client, food: true);
        var lot = NewLot();
        var batchId = await ReceiveAsync(first.Admin.Client, sku.SkuId, first.WarehouseId, 10m, InDays(60), lot);

        async Task MoveAsync(Guid from, Guid to, decimal quantity)
        {
            var transfer = await first.Admin.Client.PostAsJsonAsync("/api/staff/inventory/transfers", new
            { fromLocationId = from, toLocationId = to, reason = "Redistribute", lines = new[] { new { skuId = sku.SkuId, batchId, quantity } } }, Json);
            Assert.True(transfer.StatusCode == HttpStatusCode.Created, $"{(int)transfer.StatusCode}: {await transfer.Content.ReadAsStringAsync()}");
        }

        await MoveAsync(first.WarehouseId, first.ShopId, 4m);
        await MoveAsync(first.WarehouseId, secondWarehouse, 3m);
        return new TwoBranches(first, keeper, inSecond, secondId, secondWarehouse, sku, batchId, lot);
    }

    private static async Task<decimal> SellableAsync(Guid branchId, Guid skuId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT COALESCE(SUM(sellable_quantity), 0) FROM inventory.v_sellable_stock WHERE branch_id = @b AND sku_id = @s", connection);
        command.Parameters.AddWithValue("b", branchId);
        command.Parameters.AddWithValue("s", skuId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task I_I1_ARecallReachesABatchThatWasTransferredToAnotherBranch_SellableDropsInBothAtOnce_AndFefoSkipsIt()
    {
        var t = await HoldInTwoBranchesAsync();
        Assert.Equal(7m, await SellableAsync(t.First.BranchId, t.Sku.SkuId));  // 3 warehouse + 4 shop floor
        Assert.Equal(3m, await SellableAsync(t.SecondBranchId, t.Sku.SkuId));
        Assert.Equal(3m, (await PositionAsync(t.BatchId, t.SecondWarehouseId)).OnHand);

        var recall = await t.Keeper.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{t.BatchId}/recall",
            new { confirmLotNumber = t.Lot, reason = "Salmonella found in the lot.", customerAdvice = "Stop feeding it." }, Json);
        Assert.Equal(HttpStatusCode.Created, recall.StatusCode);

        Assert.Equal(0m, await SellableAsync(t.First.BranchId, t.Sku.SkuId));
        Assert.Equal(0m, await SellableAsync(t.SecondBranchId, t.Sku.SkuId));
        Assert.Equal(3m, (await PositionAsync(t.BatchId, t.SecondWarehouseId)).OnHand); // the stock is still there, it is just unusable

        foreach (var client in new[] { t.First.Admin.Client, t.AdminInSecond })
        {
            var preview = await JsonOkAsync(await client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={t.Sku.SkuId}&quantity=1&purpose=Sale"));
            Assert.Equal(0, preview.GetProperty("lines").GetArrayLength());
            Assert.Equal("1", preview.GetProperty("shortfall").GetString());
        }

        // the second branch's stock is refused by the consumer port AND by the database directly
        await using (var port = OpenPort())
        {
            var sale = await port.Ledger.DeductAsync(Demand(t.SecondBranchId, t.Sku.SkuId, 1m, MovementKind.Sale, locations: [t.SecondWarehouseId]),
                new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-2"), new StockParty(null, null), default);
            Assert.False(sale.IsSuccess);
            Assert.True(sale.ViolationCode is BusinessViolationCodes.InsufficientStock or BusinessViolationCodes.BatchNotUsable, sale.ViolationCode);
        }

        var refused = await InsertLedgerRowDirectlyAsync(t.SecondBranchId, t.SecondWarehouseId, t.Sku.SkuId, t.BatchId, MovementKind.Sale, -1m);
        Assert.Equal("tg_stock_batch_not_usable", refused.ConstraintName);
        Assert.Equal(3m, (await PositionAsync(t.BatchId, t.SecondWarehouseId)).OnHand);
    }

    [Fact]
    public async Task WriteOff_ByAKeeperNotAssignedToEveryHoldingBranch_IsRefusedAndWritesNothing_ButAnAdministratorOfBothMayDoIt()
    {
        var t = await HoldInTwoBranchesAsync();

        var refused = await t.Keeper.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{t.BatchId}/write-off", new { reason = "Pest damage." }, Json);

        var body = await refused.Content.ReadAsStringAsync();
        Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"{(int)refused.StatusCode}: {body}");
        Assert.Contains(ParseErrors(body), e => e.Code == BusinessViolationCodes.StockLocationNotFound);
        Assert.Equal("Available", (await JsonOkAsync(await t.Keeper.Client.GetAsync($"/api/staff/inventory/batches/{t.BatchId}"))).GetProperty("status").GetString());
        Assert.Equal(3m, (await PositionAsync(t.BatchId, t.SecondWarehouseId)).OnHand);
        Assert.Equal(3m, (await PositionAsync(t.BatchId, t.First.WarehouseId)).OnHand);

        var allowed = await t.First.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{t.BatchId}/write-off", new { reason = "Pest damage." }, Json);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(0m, (await PositionAsync(t.BatchId, t.SecondWarehouseId)).OnHand);
        Assert.Equal(0m, (await PositionAsync(t.BatchId, t.First.WarehouseId)).OnHand);
        Assert.Equal(0m, (await PositionAsync(t.BatchId, t.First.ShopId)).OnHand);
    }

    // ── the database refuses negative stock ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StockCannotGoNegative_ADirectLedgerRowLargerThanTheBalanceIsRefusedByTheCheck_AndTheBalanceIsUnchanged()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 1m);

        var refused = await InsertLedgerRowDirectlyAsync(branch.BranchId, branch.ShopId, sku.SkuId, batchId, MovementKind.Wastage, -5m);

        Assert.Equal("23514", refused.SqlState);
        Assert.Equal("ck_stock_position_balance", refused.ConstraintName);
        Assert.Equal((1m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(1, await LedgerRowCountAsync([batchId]));

        // and through the application: a loss larger than what is held is a 409, not a negative position
        var adjustment = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/adjustments", new
        {
            locationId = branch.ShopId, reason = "Theft", note = "Walked out.", lines = new[] { new { skuId = sku.SkuId, batchId, quantityDelta = -5m } }
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, adjustment.StatusCode);
        Assert.Equal(1m, (await PositionAsync(batchId, branch.ShopId)).OnHand);
    }

    // ── FI-1 against the REAL trigger and table ──────────────────────────────────────────────────────────────────────────────────

    private static async Task InsertSaleAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid branchId, Guid locationId, Guid skuId, Guid batchId)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO inventory.stock_ledger_entry
              ("Id", occurred_on, branch_id, location_id, sku_id, batch_id, kind, quantity_delta, consumes_reservation, reference_kind, reference_id, fefo_overridden, performed_by,
               "CreatedBy", "CreatedDate", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "VerticalId", "IsMarkedToDelete", "Version")
            VALUES (@id, now(), @branch, @location, @sku, @batch, @kind, -1, false, @refKind, @ref, false, @by, @by, now(), now(), @by, @by, @by, false, 1)
            """, connection, tx);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("branch", branchId);
        command.Parameters.AddWithValue("location", locationId);
        command.Parameters.AddWithValue("sku", skuId);
        command.Parameters.AddWithValue("batch", batchId);
        command.Parameters.AddWithValue("kind", MovementKind.Sale.Value);
        command.Parameters.AddWithValue("refKind", StockReferenceKind.PosSale.Value);
        command.Parameters.AddWithValue("ref", Guid.NewGuid());
        command.Parameters.AddWithValue("by", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task FI1_TheRealLedgerTriggerIsTheTwoStatementShape_AndTwoSessionsSellingTheLastUnitSerialise_OneWinnerOneCheckFailure()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 1m);

        await using (var inspect = await OpenConnectionAsync())
        await using (var def = new NpgsqlCommand("SELECT pg_get_functiondef('inventory.fn_apply_ledger_entry'::regproc)", inspect))
        {
            var body = (string)(await def.ExecuteScalarAsync())!;
            Assert.Contains("ON CONFLICT (batch_id, location_id) DO NOTHING", body);  // statement 1: ensure the row, never overwrite it
            Assert.Contains("UPDATE inventory.stock_position", body);                  // statement 2: the update takes the row lock the loser queues on
            Assert.DoesNotContain("DO UPDATE", body);                                  // the literal single-statement shape is not what is deployed
        }

        await using var c1 = await OpenConnectionAsync();
        await using var c2 = await OpenConnectionAsync();
        await using var t1 = await c1.BeginTransactionAsync();
        await using var t2 = await c2.BeginTransactionAsync();
        int pid2;
        await using (var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid()", c2, t2)) pid2 = (int)(await pidCommand.ExecuteScalarAsync())!;

        await InsertSaleAsync(c1, t1, branch.BranchId, branch.ShopId, sku.SkuId, batchId); // holds the position's row lock, uncommitted

        var second = Task.Run(async () =>
        {
            try { await InsertSaleAsync(c2, t2, branch.BranchId, branch.ShopId, sku.SkuId, batchId); return "committed-pending"; }
            catch (PostgresException ex) { return $"{ex.SqlState}:{ex.ConstraintName}"; }
        });

        // the second session must be WAITING ON A LOCK (observed in pg_stat_activity), not finished -- polled, with a ceiling, no fixed sleep
        var waiting = false;
        for (var i = 0; i < 200 && !waiting && !second.IsCompleted; i++)
        {
            await using var probe = await OpenConnectionAsync();
            await using var query = new NpgsqlCommand("SELECT wait_event_type FROM pg_stat_activity WHERE pid = @p", probe);
            query.Parameters.AddWithValue("p", pid2);
            waiting = (await query.ExecuteScalarAsync()) as string == "Lock";
            if (!waiting) await Task.Yield();
        }

        Assert.True(waiting, "FI-1: the second writer must be blocked on the first's row lock");
        Assert.False(second.IsCompleted);

        await t1.CommitAsync();
        var outcome = await second;
        await t2.RollbackAsync();

        Assert.Equal("23514:ck_stock_position_balance", outcome);
        Assert.Equal(0m, (await PositionAsync(batchId, branch.ShopId)).OnHand);
        Assert.Equal(2, await LedgerRowCountAsync([batchId])); // the receipt and exactly one sale
    }

    // ── I-I7 with SEVERAL reservations racing the recall ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I7_SixReserversHammerOneBatchWhileItIsRecalled_EveryCommittedReservationIsOnTheTrace_AndEveryReserverIsEventuallyRefused()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var committedBeforeTheRecallLanded = 0;

        for (var round = 0; round < 5; round++)
        {
            var sku = await CreateSkuAsync(branch.Admin.Client);
            var lot = NewLot();
            var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 2000m, lot: lot);
            using var gate = new ManualResetEventSlim(false);

            // each reserver plans and commits ONE unit at a time, over and over, until a plan or the commit is refused; the recall lands somewhere in the middle
            var reservers = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            {
                gate.Wait();
                var committed = 0;
                for (var attempt = 0; attempt < 1000; attempt++)
                {
                    await using var port = OpenPort();
                    var plan = await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 1m, locations: [branch.WarehouseId]), OrderReference(), Customer(), default);
                    if (!plan.IsSuccess) return (committed, "plan:" + plan.ViolationCode);
                    var outcome = await CommitOutcomeAsync(port);
                    if (outcome != "committed") return (committed, outcome);
                    committed++;
                }

                return (committed, "exhausted");
            })).ToList();
            var recall = Task.Run(async () => { gate.Wait(); await Task.Delay(40); return await RecallAsync(keeper.Client, batchId, lot); });

            gate.Set();
            var results = await Task.WhenAll(reservers);
            Assert.Equal(HttpStatusCode.Created, (await recall).StatusCode);

            Assert.All(results, r => Assert.True(
                r.Item2 == "plan:" + BusinessViolationCodes.InsufficientStock || r.Item2 == "plan:" + BusinessViolationCodes.BatchNotUsable
                    || r.Item2.StartsWith("23514:tg_stock_batch_not_usable", StringComparison.Ordinal),
                $"round {round}: a reserver ended with {r.Item2} (the recall must stop every one)"));

            var total = results.Sum(r => r.committed);
            Assert.Equal(total, (await PositionAsync(batchId, branch.WarehouseId)).Reserved);
            var trace = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}/trace"));
            Assert.Equal(total, trace.GetProperty("unshippedDispatchBlocked").GetArrayLength());
            committedBeforeTheRecallLanded += total;
        }

        output.WriteLine($"reservations committed before the recall took effect, over 5 rounds: {committedBeforeTheRecallLanded}");
        Assert.True(committedBeforeTheRecallLanded > 0, "the race never actually overlapped: no reservation committed before the recall landed");
    }

    private static Task<HttpResponseMessage> RecallAsync(HttpClient client, Guid batchId, string lot) =>
        client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/recall", new { confirmLotNumber = lot, reason = "Salmonella found in the lot.", customerAdvice = "Stop feeding it." }, Json);

    // ── the alert-raise race, a genuinely fresh key every round ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoRacingRaisesOfAFreshAlertKey_EveryRound_AllSucceed_AndExactlyOneRowPerKeyRemains()
    {
        var branch = await SeedBranchAsync();
        const int rounds = 6;
        const int racers = 8;

        for (var round = 0; round < rounds; round++)
        {
            var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
            var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 3m, InDays(15));
            var key = $"expiry:{batchId}:{branch.BranchId}:30";
            using var gate = new ManualResetEventSlim(false);

            var tasks = Enumerable.Range(0, racers).Select(_ => Task.Run(async () =>
            {
                using var scope = Factory.Services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
                gate.Wait();
                var response = await dispatcher.SendAsync(new RaiseStockAlertCommand
                {
                    AlertId = Guid.NewGuid(), Kind = "ExpiryThreshold", BranchId = branch.BranchId, SkuId = sku.SkuId, BatchId = batchId, ThresholdDays = 30, DedupKey = key,
                    UserContext = new UserContext(Guid.NewGuid(), Guid.Empty), CorrelationId = Guid.NewGuid(), ValidateCommand = true
                });
                return response.IsSuccess;
            })).ToList();
            gate.Set();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, Assert.True);
            await using var connection = await OpenConnectionAsync();
            await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM inventory.stock_alert WHERE dedup_key = @k", connection);
            count.Parameters.AddWithValue("k", key);
            Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
        }

        Assert.Equal(rounds, (await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/alerts?pageSize=100&kind=ExpiryThreshold"))).GetProperty("items").GetArrayLength());
    }

    // ── the unique-index races the ARCH gate said TEST should cover: a concurrent duplicate is a 400 with its code, never a 500 ──

    [Fact]
    public async Task EightParallelCreatesOfOneSupplierCode_OneCreated_TheRestAre400SupplierCodeAlreadyExists_NeverA500()
    {
        var branch = await SeedBranchAsync();
        var code = NewCode("SUP");
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            gate.Wait();
            var response = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/suppliers", new
            {
                code, name = "Race " + code, countryCode = "DE", contactName = "Anna", phone = "+49 30 1234", email = "anna@example.test", address = "Berlin",
                currencyCode = "EUR", leadTimeDays = 21, paymentTerms = "30 days", paymentTermDays = 30
            }, Json);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        })).ToList();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains(ParseErrors(r.Body), e => e.Code == BusinessViolationCodes.SupplierCodeAlreadyExists);
        });
    }

    [Fact]
    public async Task EightParallelOpensOfACountAtOneLocation_OneOpens_TheRestAre400CountLocationBusy_NeverA500()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m);
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            gate.Wait();
            var response = await keeper.Client.PostAsJsonAsync("/api/staff/inventory/counts", new { locationId = branch.ShopId, scope = "FullLocation", skuIds = Array.Empty<Guid>() }, Json);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        })).ToList();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK)), r =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains(ParseErrors(r.Body), e => e.Code == BusinessViolationCodes.CountLocationBusy);
        });
    }

    // ── I-D5, the reservation-entry half ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D5_TheReservationEntriesRefuseUpdateDeleteAndTruncate_InTheDatabase()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m);
        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 2m, locations: [branch.WarehouseId]), OrderReference(), Customer(), default)).IsSuccess);
            await port.CommitAsync();
        }

        async Task<PostgresException> RefusedAsync(string sql)
        {
            await using var connection = await OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        }

        Assert.Contains("immutable", (await RefusedAsync($"UPDATE inventory.stock_reservation_entry SET quantity_delta = 99 WHERE batch_id = '{batchId}'")).MessageText);
        Assert.Contains("immutable", (await RefusedAsync($"DELETE FROM inventory.stock_reservation_entry WHERE batch_id = '{batchId}'")).MessageText);
        Assert.Contains("immutable", (await RefusedAsync("TRUNCATE inventory.stock_reservation_entry")).MessageText);
        Assert.Equal(2m, (await PositionAsync(batchId, branch.WarehouseId)).Reserved);
    }
}
