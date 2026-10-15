using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 §16 phase D -- I-D1 ... I-D4, I-D7 ... I-D10, plus I-B4 and I-B6 (the PO progress that needs receipts) and I-C4 (a receipt
/// against an uncleared consignment).</summary>
public sealed class InventoryGoodsReceiptEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static readonly DateOnly InSixMonths = UtcToday.AddMonths(6);

    private static async Task<JsonElement> LedgerAsync(HttpClient client, string filter) =>
        await JsonOkAsync(await client.GetAsync($"/api/staff/inventory/ledger?from={UtcToday.AddDays(-1):yyyy-MM-dd}&to={UtcToday.AddDays(1):yyyy-MM-dd}&{filter}"));

    // ── I-D1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D1_PostingAReceiptCreatesOneBatchPerAcceptedLine_AndOneReceiptLedgerRowEach_RejectedStockStaysOutOfTheLedger()
    {
        var branch = await SeedBranchAsync();
        var food = await CreateSkuAsync(branch.Admin.Client, food: true);
        var litter = await CreateSkuAsync(branch.Admin.Client);

        var receipt = await PostReceiptAsync(branch.Admin.Client,
        [
            ReceiptLine(food.SkuId, branch.WarehouseId, accepted: 10m, expires: InSixMonths),
            ReceiptLine(litter.SkuId, branch.ShopId, accepted: 8m, rejected: 2m, rejectionReason: "Damaged"),
        ]);

        Assert.Matches(@"^GRN-\d{4}-\d{6}$", receipt.Number);
        Assert.Equal(2, receipt.BatchIds.Count);

        // Positions equal what was accepted -- the 2 rejected units are on the line, never in stock.
        Assert.Equal(10m, (await PositionAsync(receipt.BatchIds[0], branch.WarehouseId)).OnHand);
        Assert.Equal(8m, (await PositionAsync(receipt.BatchIds[1], branch.ShopId)).OnHand);
        Assert.Equal(2, await LedgerRowCountAsync(receipt.BatchIds));

        var ledger = await LedgerAsync(branch.Admin.Client, $"referenceId={receipt.ReceiptId}");
        var rows = ledger.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("Receipt", r.GetProperty("kind").GetString());
            Assert.Equal(receipt.Number, r.GetProperty("referenceLabel").GetString());
            Assert.Equal("GoodsReceipt", r.GetProperty("referenceKind").GetString());
            Assert.Equal(branch.Admin.StaffAccountId, r.GetProperty("performedBy").GetGuid());
            Assert.False(r.TryGetProperty("customerId", out _));
            Assert.False(r.TryGetProperty("catId", out _));
        });

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/goods-receipts/{receipt.ReceiptId}"));
        var partlyRejected = detail.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("skuId").GetGuid() == litter.SkuId);
        Assert.Equal("8", partlyRejected.GetProperty("quantityAccepted").GetString());
        Assert.Equal("2", partlyRejected.GetProperty("quantityRejected").GetString());
        Assert.Equal("Damaged", partlyRejected.GetProperty("rejectionReason").GetString());
        Assert.Equal("5.0000", partlyRejected.GetProperty("unitCost").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_D1_ARejectedOnlyLineMakesNoBatchAndNoLedgerRow()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);

        var receipt = await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.WarehouseId, accepted: 0m, rejected: 4m, rejectionReason: "WrongItem", lot: "")]);

        Assert.Empty(receipt.BatchIds);
        var ledger = await LedgerAsync(branch.Admin.Client, $"referenceId={receipt.ReceiptId}");
        Assert.Equal(0, ledger.GetProperty("totalCount").GetInt32());
    }

    // ── I-D2 / I-D3 / I-D4 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D2_AFoodSkuWithoutAnExpiryIsRefused_NothingIsWritten()
    {
        var branch = await SeedBranchAsync();
        var food = await CreateSkuAsync(branch.Admin.Client, food: true);

        var response = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new { lines = new[] { ReceiptLine(food.SkuId, branch.WarehouseId, 10m) } }, Json);

        await AssertErrorAsync(response, BusinessViolationCodes.ExpiryDateRequired, expectedProperty: "Lines[0].ExpiresOn");
        var receipts = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/goods-receipts"));
        Assert.Equal(0, receipts.GetProperty("totalCount").GetInt32());
        var matrix = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock/{food.SkuId}"));
        Assert.Equal(0, matrix.GetProperty("batches").GetArrayLength());
    }

    [Fact]
    public async Task I_D3_AnAlreadyExpiredExpiryIsRefused_AndEveryViolationIsReturnedTogether()
    {
        var branch = await SeedBranchAsync();
        var food = await CreateSkuAsync(branch.Admin.Client, food: true);
        var litter = await CreateSkuAsync(branch.Admin.Client);

        var response = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new
        {
            lines = new[]
            {
                ReceiptLine(food.SkuId, branch.WarehouseId, 10m, expires: UtcToday.AddDays(-1)),
                ReceiptLine(food.SkuId, branch.WarehouseId, 5m),
                ReceiptLine(litter.SkuId, branch.WarehouseId, 5m, rejected: 1m),
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = ParseErrors(await response.Content.ReadAsStringAsync());
        Assert.Contains(errors, e => e.Code == BusinessViolationCodes.BatchAlreadyExpired && e.Property == "Lines[0].ExpiresOn");
        Assert.Contains(errors, e => e.Code == BusinessViolationCodes.ExpiryDateRequired && e.Property == "Lines[1].ExpiresOn");
        Assert.Contains(errors, e => e.Code == BusinessViolationCodes.RejectionReasonRequired && e.Property == "Lines[2].RejectionReason");
    }

    [Fact]
    public async Task I_D4_ReceivingMoreThanIsOutstandingOnAPurchaseOrderLineIsRefused()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT");
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var order = await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.WarehouseId, "BDT", (sku.SkuId, 100m, 5m));

        var over = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new
        { purchaseOrderId = order.OrderId, lines = new[] { ReceiptLine(sku.SkuId, branch.WarehouseId, 101m, purchaseOrderLineId: order.LineIds[sku.SkuId]) } }, Json);

        await AssertErrorAsync(over, BusinessViolationCodes.ReceiptExceedsOutstanding);
    }

    // ── I-B4 / I-B6 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B4_ReceivingSixtyOfAHundred_LeavesFortyOutstandingAndThePurchaseOrderPartiallyReceived()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT");
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var order = await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.WarehouseId, "BDT", (sku.SkuId, 100m, 5m));

        var receipt = await PostReceiptAsync(branch.Admin.Client,
            [ReceiptLine(sku.SkuId, branch.WarehouseId, 60m, rejected: 10m, rejectionReason: "Damaged", purchaseOrderLineId: order.LineIds[sku.SkuId])], purchaseOrderId: order.OrderId);

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{order.OrderId}"));
        var line = Assert.Single(detail.GetProperty("lines").EnumerateArray());
        Assert.Equal("100", line.GetProperty("orderedQuantity").GetString());
        Assert.Equal("60", line.GetProperty("acceptedQuantity").GetString());
        Assert.Equal("10", line.GetProperty("rejectedQuantity").GetString());
        Assert.Equal("40", line.GetProperty("outstandingQuantity").GetString());
        Assert.Equal("PartiallyReceived", detail.GetProperty("receiptState").GetString());
        Assert.Equal(receipt.Number, Assert.Single(detail.GetProperty("receipts").EnumerateArray()).GetProperty("number").GetString());
        Assert.DoesNotContain("cancel", detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));

        var list = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/procurement/purchase-orders?receiptState=PartiallyReceived"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == order.OrderId);
        var none = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/procurement/purchase-orders?receiptState=FullyReceived"));
        Assert.DoesNotContain(none.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == order.OrderId);

        // The batch's cost is the PO line's (the receipt line named none).
        var batch = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}"));
        Assert.Equal("5.0000", batch.GetProperty("purchaseUnitCost").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_B6_AfterAReceiptTheOrderCannotBeCancelled_ButCanBeShortClosed_AndTheOutstandingStaysVisible()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT");
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var order = await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.WarehouseId, "BDT", (sku.SkuId, 100m, 5m));
        await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.WarehouseId, 60m, purchaseOrderLineId: order.LineIds[sku.SkuId])], purchaseOrderId: order.OrderId);

        var cancel = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{order.OrderId}/cancel", new { reason = "Changed my mind." }, Json);
        await AssertErrorAsync(cancel, BusinessViolationCodes.PurchaseOrderHasReceipts);

        var close = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{order.OrderId}/close", new { reason = "Supplier short-shipped." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{order.OrderId}"));
        Assert.Equal("Closed", detail.GetProperty("status").GetString());
        Assert.Equal("40", detail.GetProperty("lines")[0].GetProperty("outstandingQuantity").GetString());

        // A closed order takes no more goods.
        var more = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts",
            new { purchaseOrderId = order.OrderId, lines = new[] { ReceiptLine(sku.SkuId, branch.WarehouseId, 1m, purchaseOrderLineId: order.LineIds[sku.SkuId]) } }, Json);
        await AssertErrorAsync(more, BusinessViolationCodes.PurchaseOrderNotIssued);
    }

    // ── I-C4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C4_AReceiptAgainstAnAtCustomsConsignmentIsRefused_AndAgainstAClearedOneIsAccepted()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT");
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var order = await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.WarehouseId, "BDT", (sku.SkuId, 100m, 5m));

        var plan = await JsonOkAsync(await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/consignments", new
        { originCountryCode = "DE", shipmentReference = "BL-1", purchaseOrderIds = new[] { order.OrderId }, importPermitNumber = "IP", healthCertificateNumber = "HC", customsDeclarationNumber = "CD" }, Json));
        var consignmentId = plan.GetProperty("consignmentId").GetGuid();
        foreach (var to in new[] { "InTransit", "AtCustoms" })
            await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{consignmentId}/status", new { to, on = UtcToday }, Json);

        var refused = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new
        { purchaseOrderId = order.OrderId, consignmentId, lines = new[] { ReceiptLine(sku.SkuId, branch.WarehouseId, 10m, purchaseOrderLineId: order.LineIds[sku.SkuId]) } }, Json);
        await AssertErrorAsync(refused, BusinessViolationCodes.ConsignmentNotCleared);

        await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{consignmentId}/status", new { to = "Cleared", on = UtcToday }, Json);
        var receipt = await PostReceiptAsync(branch.Admin.Client,
            [ReceiptLine(sku.SkuId, branch.WarehouseId, 10m, purchaseOrderLineId: order.LineIds[sku.SkuId])], purchaseOrderId: order.OrderId, consignmentId: consignmentId);

        // The batch records the consignment it arrived on, and the inspection pack lists it with lot and expiry (US-IMP-03 AC2).
        var batch = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}"));
        Assert.Equal(consignmentId, batch.GetProperty("consignmentId").GetGuid());
        Assert.Equal("DE", batch.GetProperty("originCountryCode").GetString());

        var pack = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/consignments/{consignmentId}/inspection-pack"));
        var packBatch = Assert.Single(pack.GetProperty("batches").EnumerateArray());
        Assert.Equal(receipt.BatchIds[0], packBatch.GetProperty("batchId").GetGuid());
        Assert.Equal(receipt.Number, packBatch.GetProperty("goodsReceiptNumber").GetString());
    }

    // ── I-D5 / I-D6 / I-D7 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<PostgresException> RefusedAsync(string sql)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task I_D5_TheLedgerRefusesUpdateDeleteAndTruncate_InTheDatabase()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m);

        Assert.Contains("immutable", (await RefusedAsync($"UPDATE inventory.stock_ledger_entry SET quantity_delta = 99 WHERE batch_id = '{batchId}'")).MessageText);
        Assert.Contains("immutable", (await RefusedAsync($"DELETE FROM inventory.stock_ledger_entry WHERE batch_id = '{batchId}'")).MessageText);
        Assert.Contains("immutable", (await RefusedAsync("TRUNCATE inventory.stock_ledger_entry")).MessageText);

        Assert.Equal(1, await LedgerRowCountAsync([batchId]));
        Assert.Equal(5m, (await PositionAsync(batchId, branch.WarehouseId)).OnHand);
    }

    [Fact]
    public async Task I_D6_TheProjectionCannotBeWrittenDirectly()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m);

        var update = await RefusedAsync($"UPDATE inventory.stock_position SET on_hand = 999 WHERE batch_id = '{batchId}'");
        Assert.Equal("tg_stock_position_guard", update.ConstraintName);
        await RefusedAsync($"DELETE FROM inventory.stock_position WHERE batch_id = '{batchId}'");
        await RefusedAsync($"INSERT INTO inventory.stock_position (batch_id, location_id, sku_id, on_hand, reserved, last_movement_on) VALUES ('{batchId}', '{branch.ShopId}', '{sku.SkuId}', 50, 0, now())");
        await RefusedAsync("TRUNCATE inventory.stock_position");

        Assert.Equal(5m, (await PositionAsync(batchId, branch.WarehouseId)).OnHand);
    }

    [Fact]
    public async Task I_D7_EveryPositionEqualsTheSumOfItsLedgerEntries_AfterReceiptsAndLaterMovements()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchA = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 20m);
        var batchB = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 7m);

        var writeOff = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchB}/write-off", new { reason = "Water damage." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, writeOff.StatusCode);

        var (ledgerSum, positionSum) = await LedgerVersusPositionAsync([batchA, batchB]);
        Assert.Equal(ledgerSum, positionSum);
        Assert.Equal(20m, positionSum);
        Assert.Equal(0m, (await PositionAsync(batchB, branch.ShopId)).OnHand);
        Assert.Equal(3, await LedgerRowCountAsync([batchA, batchB]));
    }

    // ── I-D8 / I-D9 / I-D10 ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D8_TheStockMatrixShowsEachBatchAtItsLocationWithItsDerivedExpiryState()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var soon = UtcToday.AddDays(40);
        var later = UtcToday.AddDays(200);

        var batchA = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 30m, soon, "A-" + NewLot());
        var batchB = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m, later, "B-" + NewLot());
        var batchC = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.DispensaryId, 2m, later, "C-" + NewLot());

        var matrix = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock/{sku.SkuId}"));
        Assert.Equal("Asia/Dhaka", matrix.GetProperty("branchTimeZoneId").GetString());
        var batches = matrix.GetProperty("batches").EnumerateArray().ToList();
        Assert.Equal(3, batches.Count);

        // Soonest expiry first.
        Assert.Equal(batchA, batches[0].GetProperty("batchId").GetGuid());
        var a = batches[0];
        Assert.Equal(soon.ToString("yyyy-MM-dd"), a.GetProperty("expiresOn").GetString());
        Assert.False(a.GetProperty("isExpired").GetBoolean());
        Assert.Contains(a.GetProperty("daysToExpiry").GetInt32(), new[] { 39, 40 }); // the branch's local date is the UTC date or the next one
        var onlyLocation = Assert.Single(a.GetProperty("locations").EnumerateArray());
        Assert.Equal(branch.WarehouseId, onlyLocation.GetProperty("locationId").GetGuid());
        Assert.Equal("30", onlyLocation.GetProperty("onHand").GetString());
        Assert.Equal("Warehouse", onlyLocation.GetProperty("locationKind").GetString());

        Assert.Equal(new[] { batchB, batchC }.Order(), new[] { batches[1].GetProperty("batchId").GetGuid(), batches[2].GetProperty("batchId").GetGuid() }.Order());

        var summary = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock?search={sku.SkuCode}"));
        var row = Assert.Single(summary.GetProperty("items").EnumerateArray());
        Assert.Equal("37", row.GetProperty("onHand").GetString());
        Assert.Equal("0", row.GetProperty("reserved").GetString());
        Assert.Equal("37", row.GetProperty("usable").GetString());
        Assert.Equal("35", row.GetProperty("sellable").GetString()); // the dispensary does not count toward retail
        Assert.Equal(soon.ToString("yyyy-MM-dd"), row.GetProperty("nextExpiresOn").GetString());
        Assert.Equal("each", row.GetProperty("stockUnit").GetString());

        var onlyDispensary = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock?search={sku.SkuCode}&locationId={branch.DispensaryId}"));
        Assert.Equal("2", Assert.Single(onlyDispensary.GetProperty("items").EnumerateArray()).GetProperty("onHand").GetString());
    }

    [Fact]
    public async Task I_D9_ALedgerSearchNeedsARangeOfAtMost366Days_AndReturnsSkuBatchDeltaLocationReferenceAndUser()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 12m);

        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.Client.GetAsync("/api/staff/inventory/ledger")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.Client.GetAsync($"/api/staff/inventory/ledger?from={UtcToday.AddDays(-400):yyyy-MM-dd}&to={UtcToday:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.Client.GetAsync($"/api/staff/inventory/ledger?from={UtcToday:yyyy-MM-dd}&to={UtcToday.AddDays(-1):yyyy-MM-dd}")).StatusCode);

        var ledger = await LedgerAsync(branch.Admin.Client, $"batchId={batchId}");
        var row = Assert.Single(ledger.GetProperty("items").EnumerateArray());
        Assert.Equal(sku.SkuId, row.GetProperty("skuId").GetGuid());
        Assert.Equal(batchId, row.GetProperty("batchId").GetGuid());
        Assert.Equal("12", row.GetProperty("quantityDelta").GetString());
        Assert.Equal("each", row.GetProperty("stockUnit").GetString());
        Assert.Equal(branch.WarehouseId, row.GetProperty("locationId").GetGuid());
        Assert.Equal(branch.Admin.StaffAccountId, row.GetProperty("performedBy").GetGuid());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("performedByName").GetString()));
        Assert.True(row.GetProperty("sequence").GetInt64() > 0);
    }

    [Fact]
    public async Task I_D10_AReceiptLineHeldForInspectionMakesAQuarantinedBatch_ThatIsNotUsableStock()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);

        var receipt = await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.WarehouseId, 9m, hold: true)]);

        var batch = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}"));
        Assert.Equal("Quarantined", batch.GetProperty("status").GetString());
        var summary = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock?search={sku.SkuCode}"));
        var row = Assert.Single(summary.GetProperty("items").EnumerateArray());
        Assert.Equal("9", row.GetProperty("onHand").GetString());
        Assert.Equal("9", row.GetProperty("heldQuantity").GetString());
        Assert.Equal("0", row.GetProperty("usable").GetString());
        Assert.Equal("0", row.GetProperty("sellable").GetString());
    }

    // ── batch status, branch scoping, authorisation ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchStatus_QuarantineAndReleaseRoundTrip_AndEveryOtherTransitionIsRefused()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 4m);

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/quarantine", new { reason = "Torn packaging." }, Json)).StatusCode);
        await AssertErrorAsync(await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/quarantine", new { reason = "Again." }, Json), BusinessViolationCodes.InvalidBatchTransition);

        var held = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/batches/{batchId}"));
        Assert.Contains("release", held.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(2, held.GetProperty("statusHistory").GetArrayLength() + 1);

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsync($"/api/staff/inventory/batches/{batchId}/release", null)).StatusCode);
        Assert.Equal("Available", (await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/batches/{batchId}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task BatchStatus_AWriteOffMovesEveryUnitOutAsWastage_AndABatchOfAnotherBranchIsA404()
    {
        var branch = await SeedBranchAsync();
        var other = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 6m);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.Client.GetAsync($"/api/staff/inventory/batches/{batchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.Client.PostAsync($"/api/staff/inventory/batches/{batchId}/release", null)).StatusCode);

        var writeOff = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/write-off", new { reason = "Pest damage." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, writeOff.StatusCode);

        var ledger = await LedgerAsync(branch.Admin.Client, $"batchId={batchId}&kind=Wastage");
        var row = Assert.Single(ledger.GetProperty("items").EnumerateArray());
        Assert.Equal("-6", row.GetProperty("quantityDelta").GetString());
        Assert.Equal("BatchWriteOff", row.GetProperty("referenceKind").GetString());
        Assert.Equal(0m, (await PositionAsync(batchId, branch.WarehouseId)).OnHand);
        await AssertErrorAsync(await branch.Admin.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/write-off", new { reason = "Again." }, Json), BusinessViolationCodes.InvalidBatchTransition);
    }

    [Fact]
    public async Task Authorisation_ReceptionMayReadButNotReceive_AndCostsAreHiddenFromRolesWithoutValuation()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var receipt = await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.WarehouseId, 3m, cost: 12.3456m)]);
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var manager = await StaffAsync(branch, StaffRoles.ShopManager);

        var post = await reception.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new { lines = new[] { ReceiptLine(sku.SkuId, branch.WarehouseId, 1m) } }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.PostAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}/release", null)).StatusCode);

        var receptionBatch = await JsonOkAsync(await reception.Client.GetAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}"));
        Assert.Equal(JsonValueKind.Null, receptionBatch.GetProperty("purchaseUnitCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, receptionBatch.GetProperty("unitCostBase").ValueKind);
        var receptionReceipt = await JsonOkAsync(await reception.Client.GetAsync($"/api/staff/inventory/goods-receipts/{receipt.ReceiptId}"));
        Assert.Equal(JsonValueKind.Null, receptionReceipt.GetProperty("lines")[0].GetProperty("unitCost").ValueKind);

        // A shop manager holds InventoryReports.View (valuation), so sees what the goods cost -- but cannot change batch status.
        var managerBatch = await JsonOkAsync(await manager.Client.GetAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}"));
        Assert.Equal("12.3456", managerBatch.GetProperty("purchaseUnitCost").GetProperty("amount").GetString());
        Assert.Equal("Purchase", managerBatch.GetProperty("costBasis").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{receipt.BatchIds[0]}/quarantine", new { reason = "No." }, Json)).StatusCode);
    }

    [Fact]
    public async Task Locations_AnOccupiedLocationCannotBeDeactivated_AnEmptyOneCan()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 2m);

        var refused = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/locations/{branch.WarehouseId}/activation", new { isActive = false }, Json);
        await AssertErrorAsync(refused, BusinessViolationCodes.StockLocationHasStock);

        var empty = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/locations/{branch.BoardingId}/activation", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        var again = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/locations/{branch.BoardingId}/activation", new { isActive = true }, Json);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }
}
