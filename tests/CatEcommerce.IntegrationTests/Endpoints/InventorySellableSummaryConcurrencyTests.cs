using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// ADR-116 amendment: the summary is refreshed under a per-(sku, branch) lock. Two units of work that move DIFFERENT batches of the SAME two SKUs in
/// OPPOSITE orders do not conflict on any position row, so the only thing they could deadlock on is that lock. The refresh is therefore deferred to
/// commit and takes its locks for every touched (sku, branch) in one global order (sku, branch), whatever order the writer touched them in
/// (fix round 1, review HIGH). This test interleaves the two transactions step by step; on the earlier design (a lock per row, taken immediately,
/// in the writer's order) the last step is a PostgreSQL deadlock (40P01).
/// </summary>
public sealed class InventorySellableSummaryConcurrencyTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static DateOnly InDays(int days) => UtcToday.AddDays(days);

    private static async Task<(decimal Live, decimal Summary)> SellableAsync(Guid branchId, Guid skuId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE((SELECT SUM(sellable_quantity) FROM inventory.v_sellable_stock WHERE branch_id = @b AND sku_id = @s), 0), " +
            "COALESCE((SELECT SUM(sellable_quantity) FROM inventory.v_sku_sellable WHERE branch_id = @b AND sku_id = @s), 0)", connection);
        command.Parameters.AddWithValue("b", branchId);
        command.Parameters.AddWithValue("s", skuId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    [Fact]
    public async Task TwoTransactions_MovingDifferentBatchesOfTheSameTwoSkus_InOppositeOrders_DoNotDeadlock()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var sku1 = await CreateSkuAsync(admin, food: true);
        var sku2 = await CreateSkuAsync(admin, food: true);

        // Two batches per SKU at the shop floor: each transaction takes its own batch, so no position row is shared.
        var x1 = await ReceiveAsync(admin, sku1.SkuId, branch.ShopId, 10m, InDays(30));
        var x2 = await ReceiveAsync(admin, sku1.SkuId, branch.ShopId, 10m, InDays(60));
        var y1 = await ReceiveAsync(admin, sku2.SkuId, branch.ShopId, 10m, InDays(30));
        var y2 = await ReceiveAsync(admin, sku2.SkuId, branch.ShopId, 10m, InDays(60));
        const string reason = "Each unit of work takes a named batch.";

        await using var a = OpenPort();
        await using var b = OpenPort();
        await using var txA = await a.Db.Database.BeginTransactionAsync();
        await using var txB = await b.Db.Database.BeginTransactionAsync();

        async Task ReserveAsync(Port port, Guid skuId, Guid batchId)
        {
            var result = await port.Ledger.ReserveAsync(Demand(branch.BranchId, skuId, 1m, lines: [(batchId, branch.ShopId, 1m)], reason: reason), OrderReference(), Customer(), default);
            Assert.True(result.IsSuccess, result.Message);
            await port.CommitAsync(); // SaveChanges inside the open transaction: the rows are written, nothing is committed
        }

        await ReserveAsync(a, sku1.SkuId, x1); // A: SKU 1 first
        await ReserveAsync(b, sku2.SkuId, y2); // B: SKU 2 first
        var aSecond = ReserveAsync(a, sku2.SkuId, y1); // A then SKU 2 (the earlier design waits here for B's lock, so it is not awaited yet)
        await Task.Delay(1000);
        var bSecond = ReserveAsync(b, sku1.SkuId, x2); // B then SKU 1 (the earlier design: deadlock, 40P01)
        await Task.WhenAll(aSecond, bSecond).WaitAsync(TimeSpan.FromSeconds(60));

        // The commits run together: each flushes its touched pairs in (sku, branch) order.
        await Task.WhenAll(txA.CommitAsync(), txB.CommitAsync());

        foreach (var sku in new[] { sku1.SkuId, sku2.SkuId })
        {
            var (live, summary) = await SellableAsync(branch.BranchId, sku);
            Assert.Equal(18m, live);
            Assert.Equal(live, summary);
        }
    }
}
