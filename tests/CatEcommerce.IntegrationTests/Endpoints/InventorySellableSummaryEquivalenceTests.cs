using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Shared.Common.Enums;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// ADR-116 amendment (owner, 2026-10-07; HO-SHP-001-FU2): <c>inventory.v_sku_sellable</c>, backed by the trigger-kept
/// <c>inventory.sku_sellable_summary</c>, must equal <c>inventory.v_sellable_stock</c> at every instant. After EACH step of a mixed history -- receipts,
/// a reservation and its release, quarantine and release, a transfer out of and back into sellable stock, a recall, a write-off, a location going
/// inactive, a stock adjustment, and an expiry boundary crossed with no stock write at all -- the two are compared in ONE statement (one snapshot, one
/// <c>now()</c>), both for the test's branch and database-wide: zero differences, over a non-empty set.
/// </summary>
public sealed class InventorySellableSummaryEquivalenceTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string DifferencesSql = """
        WITH a AS (SELECT branch_id, sku_id, sellable_quantity, next_expires_on FROM inventory.v_sellable_stock),
             b AS (SELECT branch_id, sku_id, SUM(sellable_quantity) AS quantity, MIN(expires_on) FILTER (WHERE sellable_quantity > 0) AS next_expires_on
                     FROM inventory.v_sku_sellable GROUP BY branch_id, sku_id)
        SELECT COUNT(*) FILTER (WHERE a.sku_id IS NULL OR b.sku_id IS NULL OR a.sellable_quantity IS DISTINCT FROM b.quantity OR a.next_expires_on IS DISTINCT FROM b.next_expires_on),
               COUNT(*) FILTER (WHERE COALESCE(a.branch_id, b.branch_id) = @branch),
               COUNT(*)
          FROM a FULL OUTER JOIN b ON a.branch_id = b.branch_id AND a.sku_id = b.sku_id
        """;

    private static async Task AssertEqualAsync(Guid branchId, string step)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(DifferencesSql, connection);
        command.Parameters.AddWithValue("branch", branchId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var (differences, ownRows, allRows) = (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));

        Assert.True(differences == 0, $"after '{step}': {differences} of {allRows} (branch, sku) rows differ between v_sellable_stock and v_sku_sellable");
        Assert.True(ownRows > 0, $"after '{step}': the comparison covered none of this test's rows");
    }

    private static async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<decimal> SellableOfBatchDateAsync(Guid skuId, Guid branchId, DateOnly expiresOn)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(SUM(sellable_quantity), 0) FROM inventory.v_sku_sellable WHERE sku_id = @s AND branch_id = @b AND expires_on = @d", connection);
        command.Parameters.AddWithValue("s", skuId);
        command.Parameters.AddWithValue("b", branchId);
        command.Parameters.AddWithValue("d", expiresOn);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private static DateOnly InDays(int days) => UtcToday.AddDays(days);

    [Fact]
    public async Task TheSummary_EqualsTheLiveView_AfterEveryStepOfAMixedHistory()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var food = await CreateSkuAsync(admin, food: true);
        var plain = await CreateSkuAsync(admin);

        // Receipts: dated food at the shop floor and the warehouse, undated stock, plus stock that must NOT count (quarantine location, dispensary).
        var shopBatch = await ReceiveAsync(admin, food.SkuId, branch.ShopId, 10m, InDays(45), "LOTSHOP");
        var warehouseBatch = await ReceiveAsync(admin, food.SkuId, branch.WarehouseId, 6m, InDays(20), "LOTWH");
        var plainBatch = await ReceiveAsync(admin, plain.SkuId, branch.ShopId, 8m);
        await ReceiveAsync(admin, food.SkuId, branch.QuarantineId, 9m, InDays(80));
        await ReceiveAsync(admin, plain.SkuId, branch.DispensaryId, 5m);
        await AssertEqualAsync(branch.BranchId, "receipts");

        // A reservation (on-hand unchanged, sellable down) and its release.
        var order = OrderReference();
        await using (var port = OpenPort())
        {
            var reserved = await port.Ledger.ReserveAsync(Demand(branch.BranchId, food.SkuId, 3m, lines: [(shopBatch, branch.ShopId, 3m)], reason: "The customer asked for the freshest bags."), order, Customer(), default);
            Assert.True(reserved.IsSuccess, reserved.Message);
            await port.CommitAsync();
        }

        await AssertEqualAsync(branch.BranchId, "reservation");

        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.ReleaseAsync(order, default)).IsSuccess);
            await port.CommitAsync();
        }

        await AssertEqualAsync(branch.BranchId, "release of the reservation");

        // Quarantine and release of a batch.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/staff/inventory/batches/{warehouseBatch}/quarantine", new { reason = "Torn bags." }, Json)).StatusCode);
        await AssertEqualAsync(branch.BranchId, "quarantine");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/staff/inventory/batches/{warehouseBatch}/release", null)).StatusCode);
        await AssertEqualAsync(branch.BranchId, "release of the batch");

        // A transfer out of sellable stock (shop floor -> dispensary) and back.
        var out1 = await admin.PostAsJsonAsync("/api/staff/inventory/transfers", new
        { fromLocationId = branch.ShopId, toLocationId = branch.DispensaryId, reason = "Restock", lines = new[] { new { skuId = food.SkuId, batchId = shopBatch, quantity = 4m } } }, Json);
        await JsonOkAsync(out1);
        await AssertEqualAsync(branch.BranchId, "transfer to the dispensary");
        var back = await admin.PostAsJsonAsync("/api/staff/inventory/transfers", new
        { fromLocationId = branch.DispensaryId, toLocationId = branch.ShopId, reason = "Restock", lines = new[] { new { skuId = food.SkuId, batchId = shopBatch, quantity = 4m } } }, Json);
        await JsonOkAsync(back);
        await AssertEqualAsync(branch.BranchId, "transfer back to the shop floor");

        // A stock adjustment.
        var adjust = await admin.PostAsJsonAsync("/api/staff/inventory/adjustments", new
        { locationId = branch.ShopId, reason = "Damage", note = "Water damage in the store.", lines = new[] { new { skuId = plain.SkuId, batchId = plainBatch, quantityDelta = -2m } } }, Json);
        await JsonOkAsync(adjust);
        await AssertEqualAsync(branch.BranchId, "adjustment");

        // Sale paths: a FEFO deduction (Sale) and a reservation consumed into a Sale -- the consuming ledger entries, also on undated (infinity-bucket) stock.
        await using (var port = OpenPort())
        {
            var deducted = await port.Ledger.DeductAsync(Demand(branch.BranchId, plain.SkuId, 1m, MovementKind.Sale), OrderReference(), Customer(), default);
            Assert.True(deducted.IsSuccess, deducted.Message);
            await port.CommitAsync();
        }

        await AssertEqualAsync(branch.BranchId, "sale (deduct)");

        var saleOrder = OrderReference();
        await using (var port = OpenPort())
        {
            var reserved = await port.Ledger.ReserveAsync(Demand(branch.BranchId, plain.SkuId, 2m), saleOrder, Customer(), default);
            Assert.True(reserved.IsSuccess, reserved.Message);
            await port.CommitAsync();
        }

        await AssertEqualAsync(branch.BranchId, "reservation for a sale");
        await using (var port = OpenPort())
        {
            var consumed = await port.Ledger.ConsumeReservationAsync(saleOrder, Customer(), default);
            Assert.True(consumed.IsSuccess, consumed.Message);
            await port.CommitAsync();
        }

        await AssertEqualAsync(branch.BranchId, "reservation consumed into a sale");

        // A location moving to another branch (its stock follows it) and back.
        var otherBranch = await SeedBranchAsync(withLocations: false);
        await ExecuteAsync("UPDATE inventory.stock_location SET branch_id = @o WHERE \"Id\" = @l", ("o", otherBranch.BranchId), ("l", branch.WarehouseId));
        await AssertEqualAsync(branch.BranchId, "warehouse moved to another branch");
        await ExecuteAsync("UPDATE inventory.stock_location SET branch_id = @o WHERE \"Id\" = @l", ("o", branch.BranchId), ("l", branch.WarehouseId));
        await AssertEqualAsync(branch.BranchId, "warehouse moved back");

        // A location going inactive and active again (the sellable kind is a property of the LOCATION).
        await ExecuteAsync("UPDATE inventory.stock_location SET is_active = false WHERE \"Id\" = @l", ("l", branch.WarehouseId));
        await AssertEqualAsync(branch.BranchId, "warehouse deactivated");
        await ExecuteAsync("UPDATE inventory.stock_location SET is_active = true WHERE \"Id\" = @l", ("l", branch.WarehouseId));
        await AssertEqualAsync(branch.BranchId, "warehouse reactivated");

        // Expiry date edited directly (an expired batch) and back.
        await ExecuteAsync("UPDATE inventory.batch SET expires_on = @d WHERE \"Id\" = @b", ("d", InDays(-3)), ("b", warehouseBatch));
        await AssertEqualAsync(branch.BranchId, "a batch expiring in the past");
        await ExecuteAsync("UPDATE inventory.batch SET expires_on = @d WHERE \"Id\" = @b", ("d", InDays(20)), ("b", warehouseBatch));
        await AssertEqualAsync(branch.BranchId, "the expiry restored");

        // The time-based boundary, crossed with NO stock write: a batch usable through its whole expiry date in the branch local date. The
        // branch moves between the earliest and the latest time zone, so its local date jumps a day with nothing in Inventory changing.
        await ExecuteAsync("UPDATE admin.branch SET time_zone_id = @z WHERE \"Id\" = @b", ("z", "Pacific/Pago_Pago"), ("b", branch.BranchId));
        var pagoPagoToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-11));
        var boundary = await ReceiveAsync(admin, food.SkuId, branch.ShopId, 2m, pagoPagoToday, "LOTBOUND"); // usable through its whole date there, at any UTC hour
        foreach (var zone in new[] { "Pacific/Pago_Pago", "Pacific/Kiritimati", "Pacific/Pago_Pago", "Asia/Dhaka", "Pacific/Kiritimati" })
        {
            await ExecuteAsync("UPDATE admin.branch SET time_zone_id = @z WHERE \"Id\" = @b", ("z", zone), ("b", branch.BranchId));
            await AssertEqualAsync(branch.BranchId, $"branch time zone {zone}");

            // The boundary really moves: the 2 units are sellable while the branch date is the batch date, and not once it is a day later.
            var expected = zone == "Pacific/Kiritimati" ? 0m : 2m;
            if (zone is "Pacific/Pago_Pago" or "Pacific/Kiritimati")
                Assert.Equal(expected, await SellableOfBatchDateAsync(food.SkuId, branch.BranchId, pagoPagoToday));
        }

        // Write-off and recall of batches.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/staff/inventory/batches/{boundary}/write-off", new { reason = "Water damage." }, Json)).StatusCode);
        await AssertEqualAsync(branch.BranchId, "write-off");
        var recall = await admin.PostAsJsonAsync($"/api/staff/inventory/batches/{shopBatch}/recall",
            new { confirmLotNumber = "LOTSHOP", reason = "Salmonella found in the lot.", customerAdvice = "Stop feeding it and keep the packaging." }, Json);
        await JsonOkAsync(recall);
        await AssertEqualAsync(branch.BranchId, "recall");
    }

    [Fact]
    public async Task TheSummary_CannotBeWrittenDirectly()
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "INSERT INTO inventory.sku_sellable_summary (sku_id, branch_id, expires_on, sellable_quantity, last_changed_on) VALUES (@s, @b, 'infinity', 1, now())",
            ("s", Guid.NewGuid()), ("b", Guid.NewGuid())));

        Assert.Equal("42501", exception.SqlState);
    }
}
