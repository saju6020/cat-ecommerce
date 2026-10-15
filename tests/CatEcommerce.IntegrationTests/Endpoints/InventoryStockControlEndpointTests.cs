using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 §16 phase F -- I-F1 ... I-F9 (and I-E11): transfers, adjustments with the approval threshold, blind counts and the exact commit.
/// Every test has its own branch, locations and SKUs; triggers and constraints are real.
/// </summary>
public sealed class InventoryStockControlEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string Transfers = "/api/staff/inventory/transfers";
    private const string Adjustments = "/api/staff/inventory/adjustments";
    private const string Counts = "/api/staff/inventory/counts";

    private async Task<(BranchContext Branch, StaffClient Keeper, SeededSku Sku)> SetupAsync()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        return (branch, keeper, sku);
    }

    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, Guid from, Guid to, Guid skuId, Guid batchId, decimal quantity) =>
        client.PostAsJsonAsync(Transfers, new { fromLocationId = from, toLocationId = to, reason = "Restock", lines = new[] { new { skuId, batchId, quantity } } }, Json);

    // ── I-F1 / I-F2 / I-F3: transfers ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F1_ATransferMovesStockBetweenLocations_AsOneLedgerPairPerLine_AndTheLedgerStillBalances()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m);

        var response = await TransferAsync(keeper.Client, branch.WarehouseId, branch.ShopId, sku.SkuId, batchId, 4m);

        var body = await JsonOkAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith($"TRF-{DateTime.UtcNow.Year}-", body.GetProperty("number").GetString());
        Assert.Equal((6m, 0m), await PositionAsync(batchId, branch.WarehouseId));
        Assert.Equal((4m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(3, await LedgerRowCountAsync([batchId])); // receipt + out + in
        var (ledger, position) = await LedgerVersusPositionAsync([batchId]);
        Assert.Equal(ledger, position);
        Assert.Equal(10m, ledger);

        var detail = await JsonOkAsync(await keeper.Client.GetAsync($"{Transfers}/{body.GetProperty("transferId").GetGuid()}"));
        Assert.Equal(branch.WarehouseId, detail.GetProperty("fromLocationId").GetGuid());
        Assert.Equal("4", Assert.Single(detail.GetProperty("lines").EnumerateArray()).GetProperty("quantity").GetString());
    }

    [Fact]
    public async Task I_F2_TwoBatchesAtThreeLocations_TheTransferTouchesOnlyItsOwnBatchAndLocations()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchA = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m);
        var batchB = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 7m);
        await TransferAsync(keeper.Client, branch.WarehouseId, branch.DispensaryId, sku.SkuId, batchA, 3m);

        Assert.Equal((7m, 0m), await PositionAsync(batchA, branch.WarehouseId));
        Assert.Equal((3m, 0m), await PositionAsync(batchA, branch.DispensaryId));
        Assert.Equal((0m, 0m), await PositionAsync(batchA, branch.ShopId));
        Assert.Equal((7m, 0m), await PositionAsync(batchB, branch.ShopId));
        Assert.Equal((0m, 0m), await PositionAsync(batchB, branch.WarehouseId));
        var (ledger, position) = await LedgerVersusPositionAsync([batchA, batchB]);
        Assert.Equal(ledger, position);
        Assert.Equal(17m, ledger);
    }

    [Fact]
    public async Task I_F3_ATransferToTheSameLocation_OrWithTheStaffRoleOfAReceptionist_IsRefused()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m);

        await AssertErrorAsync(await TransferAsync(keeper.Client, branch.WarehouseId, branch.WarehouseId, sku.SkuId, batchId, 1m), BusinessViolationCodes.TransferLocationsMustDiffer);

        var reception = await StaffAsync(branch, StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await TransferAsync(reception.Client, branch.WarehouseId, branch.ShopId, sku.SkuId, batchId, 1m)).StatusCode);
        Assert.Equal((10m, 0m), await PositionAsync(batchId, branch.WarehouseId));
    }

    // ── I-F4 / I-E11: the stock is no longer free ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F4_AndI_E11_ATransferOfStockThatMovedAway_Is409InsufficientStock_WithTheSharedMessage_AndWritesNothing()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m);

        await using (var pos = OpenPort())
        {
            var deducted = await pos.Ledger.DeductAsync(
                Demand(branch.BranchId, sku.SkuId, 8m, MovementKind.Sale, locations: [branch.WarehouseId]),
                new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-1"), new StockParty(null, null), default);
            Assert.True(deducted.IsSuccess, deducted.Message);
            await pos.CommitAsync();
        }

        var before = await LedgerRowCountAsync([batchId]);
        var response = await TransferAsync(keeper.Client, branch.WarehouseId, branch.ShopId, sku.SkuId, batchId, 5m);

        await AssertErrorAsync(response, BusinessViolationCodes.InsufficientStock, HttpStatusCode.Conflict);
        Assert.Contains("available", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await LedgerRowCountAsync([batchId]));
        Assert.Equal((2m, 0m), await PositionAsync(batchId, branch.WarehouseId));
        Assert.Equal((0m, 0m), await PositionAsync(batchId, branch.ShopId));
    }

    // ── I-F5 / I-F6: adjustments ───────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<HttpResponseMessage> AdjustAsync(HttpClient client, Guid locationId, Guid skuId, Guid batchId, decimal delta, string? reason = "Damage", string? note = "Water damage in the store.") =>
        client.PostAsJsonAsync(Adjustments, new { locationId, reason, note, lines = new[] { new { skuId, batchId, quantityDelta = delta } } }, Json);

    [Fact]
    public async Task I_F5_ASmallAdjustmentPostsAtOnce_AndAReasonAndNoteAreMandatory()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);

        await AssertErrorAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -1m, reason: null), BusinessViolationCodes.AdjustmentReasonRequired);
        await AssertErrorAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -1m, note: null), BusinessViolationCodes.AdjustmentNoteRequired);
        Assert.Equal((10m, 0m), await PositionAsync(batchId, branch.ShopId));

        var body = await JsonOkAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -3m));

        Assert.Equal("Posted", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal((7m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(2, await LedgerRowCountAsync([batchId]));
    }

    [Fact]
    public async Task I_F6_AnAdjustmentOverTheThresholdWritesNothingUntilADifferentApproverApproves()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var receipt = await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.ShopId, 100m, cost: 100m)]);
        var batchId = Assert.Single(receipt.BatchIds);

        var requested = await JsonOkAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -60m)); // 60 x 100 = 6000 > 5000
        var id = requested.GetProperty("adjustmentId").GetGuid();

        Assert.Equal("PendingApproval", requested.GetProperty("status").GetString());
        Assert.True(requested.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal((100m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(1, await LedgerRowCountAsync([batchId]));

        // the requester holds no approval action; the administrator (a different person) does
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.Client.PostAsJsonAsync($"{Adjustments}/{id}/approve", new { note = "ok" }, Json)).StatusCode);
        var approve = await branch.Admin.Client.PostAsJsonAsync($"{Adjustments}/{id}/approve", new { note = "Checked." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        Assert.Equal((40m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(2, await LedgerRowCountAsync([batchId]));
        await AssertErrorAsync(await branch.Admin.Client.PostAsJsonAsync($"{Adjustments}/{id}/approve", new { note = "again" }, Json), BusinessViolationCodes.AdjustmentNotPending);
    }

    [Fact]
    public async Task I_F6_RejectAndWithdrawLeaveTheLedgerUntouched_AndOnlyTheRequesterMayWithdraw()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = Assert.Single((await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.ShopId, 100m, cost: 100m)])).BatchIds);
        var rejectId = (await JsonOkAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -60m))).GetProperty("adjustmentId").GetGuid();
        var withdrawId = (await JsonOkAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -55m))).GetProperty("adjustmentId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsJsonAsync($"{Adjustments}/{rejectId}/reject", new { note = "Not justified." }, Json)).StatusCode);
        await AssertErrorAsync(await branch.Admin.Client.PostAsync($"{Adjustments}/{withdrawId}/withdraw", null), BusinessViolationCodes.AdjustmentWithdrawNotRequester);
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsync($"{Adjustments}/{withdrawId}/withdraw", null)).StatusCode);

        Assert.Equal((100m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(1, await LedgerRowCountAsync([batchId]));
        Assert.Equal("Rejected", (await JsonOkAsync(await keeper.Client.GetAsync($"{Adjustments}/{rejectId}"))).GetProperty("status").GetString());
        Assert.Equal("Withdrawn", (await JsonOkAsync(await keeper.Client.GetAsync($"{Adjustments}/{withdrawId}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_F6_ApprovingAfterTheStockHasMoved_Is409InsufficientStock_AndTheAdjustmentStaysPending()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = Assert.Single((await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.ShopId, 100m, cost: 100m)])).BatchIds);
        var id = (await JsonOkAsync(await AdjustAsync(keeper.Client, branch.ShopId, sku.SkuId, batchId, -60m))).GetProperty("adjustmentId").GetGuid();

        await using (var pos = OpenPort())
        {
            Assert.True((await pos.Ledger.DeductAsync(
                Demand(branch.BranchId, sku.SkuId, 70m, MovementKind.Sale, locations: [branch.ShopId]),
                new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-9"), new StockParty(null, null), default)).IsSuccess);
            await pos.CommitAsync();
        }

        var response = await branch.Admin.Client.PostAsJsonAsync($"{Adjustments}/{id}/approve", new { note = "late" }, Json);

        await AssertErrorAsync(response, BusinessViolationCodes.InsufficientStock, HttpStatusCode.Conflict);
        Assert.Equal((30m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal("PendingApproval", (await JsonOkAsync(await keeper.Client.GetAsync($"{Adjustments}/{id}"))).GetProperty("status").GetString());
    }

    // ── I-F7 / I-F8 / I-F9: counts ──────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> OpenCountAsync(HttpClient client, Guid locationId, string scope = "FullLocation", IReadOnlyList<Guid>? skuIds = null)
    {
        var response = await client.PostAsJsonAsync(Counts, new { locationId, scope, skuIds = skuIds ?? [] }, Json);
        return (await JsonOkAsync(response)).GetProperty("countId").GetGuid();
    }

    [Fact]
    public async Task I_F7_TheCountSheetIsBlindWhileOpen_AndSubmitFreezesTheVarianceWithNothingInTheLedger()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var counted = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);
        var missed = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 7m);
        var countId = await OpenCountAsync(keeper.Client, branch.ShopId);

        var put = await keeper.Client.PutAsJsonAsync($"{Counts}/{countId}/lines", new { lines = new[] { new { skuId = sku.SkuId, batchId = counted, countedQuantity = 8m } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var open = await JsonOkAsync(await keeper.Client.GetAsync($"{Counts}/{countId}"));
        Assert.True(open.GetProperty("isBlind").GetBoolean());
        var line = Assert.Single(open.GetProperty("lines").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("bookQuantity").ValueKind);
        Assert.Equal(JsonValueKind.Null, line.GetProperty("variance").ValueKind);
        Assert.DoesNotContain("10", line.GetProperty("countedQuantity").GetString()!);

        var before = await LedgerRowCountAsync([counted, missed]);
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsync($"{Counts}/{countId}/submit", null)).StatusCode);
        Assert.Equal(before, await LedgerRowCountAsync([counted, missed])); // US-IMP-14 AC1

        var variance = await JsonOkAsync(await keeper.Client.GetAsync($"{Counts}/{countId}/variance"));
        var lines = variance.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count); // the uncounted position of 7 became a line counted 0
        Assert.Equal("-2", lines.Single(l => l.GetProperty("batchId").GetGuid() == counted).GetProperty("variance").GetString());
        Assert.Equal("-7", lines.Single(l => l.GetProperty("batchId").GetGuid() == missed).GetProperty("variance").GetString());
        Assert.False(variance.GetProperty("willNeedApproval").GetBoolean());

        var submitted = await JsonOkAsync(await keeper.Client.GetAsync($"{Counts}/{countId}"));
        Assert.False(submitted.GetProperty("isBlind").GetBoolean());
    }

    [Fact]
    public async Task I_F7_CommitPostsExactlyTheReportedVariances_AndTheLedgerStillBalances()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var loss = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);
        var gain = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m);
        var same = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 4m);
        var countId = await OpenCountAsync(keeper.Client, branch.ShopId);
        await keeper.Client.PutAsJsonAsync($"{Counts}/{countId}/lines", new
        {
            lines = new[]
            {
                new { skuId = sku.SkuId, batchId = loss, countedQuantity = 9m }, new { skuId = sku.SkuId, batchId = gain, countedQuantity = 6m },
                new { skuId = sku.SkuId, batchId = same, countedQuantity = 4m }
            }
        }, Json);
        await keeper.Client.PostAsync($"{Counts}/{countId}/submit", null);

        var commit = await JsonOkAsync(await keeper.Client.PostAsync($"{Counts}/{countId}/commit", null));

        Assert.Equal("Committed", commit.GetProperty("status").GetString());
        Assert.Equal((9m, 0m), await PositionAsync(loss, branch.ShopId));
        Assert.Equal((6m, 0m), await PositionAsync(gain, branch.ShopId));
        Assert.Equal((4m, 0m), await PositionAsync(same, branch.ShopId));
        Assert.Equal(2, await LedgerRowCountAsync([loss]));
        Assert.Equal(2, await LedgerRowCountAsync([gain]));
        Assert.Equal(1, await LedgerRowCountAsync([same])); // a matching line posts nothing
        var (ledger, position) = await LedgerVersusPositionAsync([loss, gain, same]);
        Assert.Equal(ledger, position);
    }

    [Fact]
    public async Task I_F8_OnlyOneActiveCountPerLocation_AndAVarianceOverTheThresholdAwaitsAnotherApprover()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = Assert.Single((await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.ShopId, 100m, cost: 100m)])).BatchIds);
        var countId = await OpenCountAsync(keeper.Client, branch.ShopId);

        await AssertErrorAsync(
            await keeper.Client.PostAsJsonAsync(Counts, new { locationId = branch.ShopId, scope = "FullLocation", skuIds = Array.Empty<Guid>() }, Json),
            BusinessViolationCodes.CountLocationBusy);

        await keeper.Client.PutAsJsonAsync($"{Counts}/{countId}/lines", new { lines = new[] { new { skuId = sku.SkuId, batchId, countedQuantity = 0m } } }, Json);
        await keeper.Client.PostAsync($"{Counts}/{countId}/submit", null);
        Assert.True((await JsonOkAsync(await keeper.Client.GetAsync($"{Counts}/{countId}/variance"))).GetProperty("willNeedApproval").GetBoolean());

        var commit = await JsonOkAsync(await keeper.Client.PostAsync($"{Counts}/{countId}/commit", null));
        Assert.Equal("AwaitingApproval", commit.GetProperty("status").GetString());
        Assert.Equal((100m, 0m), await PositionAsync(batchId, branch.ShopId));

        // a count awaiting approval still occupies the location
        await AssertErrorAsync(
            await keeper.Client.PostAsJsonAsync(Counts, new { locationId = branch.ShopId, scope = "FullLocation", skuIds = Array.Empty<Guid>() }, Json),
            BusinessViolationCodes.CountLocationBusy);

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsync($"{Counts}/{countId}/approve", null)).StatusCode);
        Assert.Equal((0m, 0m), await PositionAsync(batchId, branch.ShopId));

        // committed, so the location is free again
        Assert.NotEqual(Guid.Empty, await OpenCountAsync(keeper.Client, branch.ShopId));
    }

    [Fact]
    public async Task I_F9_ACancelledCountFreesTheLocation_AndWritesNothing()
    {
        var (branch, keeper, sku) = await SetupAsync();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);
        var countId = await OpenCountAsync(keeper.Client, branch.ShopId);
        await keeper.Client.PutAsJsonAsync($"{Counts}/{countId}/lines", new { lines = new[] { new { skuId = sku.SkuId, batchId, countedQuantity = 1m } } }, Json);

        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsync($"{Counts}/{countId}/cancel", null)).StatusCode);

        Assert.Equal((10m, 0m), await PositionAsync(batchId, branch.ShopId));
        Assert.Equal(1, await LedgerRowCountAsync([batchId]));
        await AssertErrorAsync(await keeper.Client.PostAsync($"{Counts}/{countId}/submit", null), BusinessViolationCodes.InvalidCountTransition);
        Assert.NotEqual(Guid.Empty, await OpenCountAsync(keeper.Client, branch.ShopId));
    }
}
