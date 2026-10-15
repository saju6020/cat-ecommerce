using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 §11.4 C8/C9 -- the orchestration of posting a receipt: what is read, what is enlisted, what is saved, and that nothing is
/// enlisted or saved when anything is wrong. The line rules themselves are the aggregate's (<c>GoodsReceiptTests</c>).</summary>
public sealed class GoodsReceiptHandlerTests
{
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 7);

    private sealed class Rig
    {
        public FakeAggregateRootRepository<GoodsReceiptAggregate> Receipts { get; } = new();
        public FakeAggregateRootRepository<PurchaseOrderAggregate> Orders { get; } = new();
        public FakeAggregateRootRepository<SupplierAggregate> Suppliers { get; } = new();
        public FakeAggregateRootRepository<ConsignmentAggregate> Consignments { get; } = new();
        public FakeSkuDirectory Skus { get; } = new();
        public FakeStockPositionReader Positions { get; } = new();
        public FakeBatchRegistry Batches { get; } = new();
        public FakeLedgerWriter Ledger { get; } = new();
        public Domain.Models.StockLocationFact Warehouse { get; }

        public Rig() => Warehouse = Positions.AddLocation(Branch, StockLocationKind.Warehouse);

        public PostGoodsReceiptCommandHandler Handler() => new(
            NullLogger<PostGoodsReceiptCommandHandler>.Instance, Receipts, Orders, Suppliers, Consignments, Skus, Positions, new FakeInventoryClock(Today), Batches, Ledger,
            new FakeDocumentNumbers(), new InMemoryBusinessSettings(new Dictionary<string, object> { [SettingKeys.CommerceCurrencyCode] = "BDT" }), TimeProvider.System);

        public PostGoodsReceiptCommand Command(params GoodsReceiptLineInput[] lines) => new()
        {
            GoodsReceiptId = Guid.NewGuid(), BranchId = Branch, Lines = lines, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        public GoodsReceiptLineInput Line(
            SkuStockProfile sku, decimal accepted = 10m, decimal rejected = 0m, DateOnly? expires = null, string? reason = null, bool hold = false) =>
            new(Guid.NewGuid(), sku.SkuId, null, "LOT-" + Guid.NewGuid().ToString("N")[..6], null, expires, accepted, rejected, reason, null, Warehouse.LocationId, hold, 5m, "BDT", null, Guid.NewGuid());
    }

    [Fact]
    public async Task APostedReceipt_SavesOnceAndEnlistsOneBatchAndOneReceiptDraftPerAcceptedLine()
    {
        var rig = new Rig();
        var food = rig.Skus.Add(expiryRequired: true, stockClass: StockClass.Food);
        var litter = rig.Skus.Add();

        var response = await rig.Handler().HandleAsync(rig.Command(rig.Line(food, expires: Today.AddMonths(6)), rig.Line(litter, accepted: 8m, rejected: 2m, reason: "Damaged")));

        Assert.True(response.IsSuccess, string.Join("; ", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
        var receipt = Assert.Single(rig.Receipts.Saved);
        Assert.Equal(2, rig.Batches.Enlisted.Count);
        Assert.Equal(2, rig.Ledger.Appended.Count);
        Assert.All(rig.Ledger.Appended, d =>
        {
            Assert.Equal(MovementKind.Receipt, d.Kind);
            Assert.Equal(StockReferenceKind.GoodsReceipt, d.ReferenceKind);
            Assert.Equal(receipt.Id, d.ReferenceId);
            Assert.Equal(receipt.Number, d.ReferenceLabel);
            Assert.True(d.QuantityDelta > 0m);
        });
        Assert.Equal([8m, 10m], rig.Ledger.Appended.Select(d => d.QuantityDelta).Order());

        var posted = (PostedGoodsReceipt)response.Result!;
        Assert.Equal(rig.Batches.Enlisted.Select(b => b.Id), posted.BatchIds);
        Assert.All(rig.Batches.Enlisted, b => Assert.Equal(BatchStatus.Available, b.Status));
    }

    [Fact]
    public async Task ARejectedOnlyLine_EnlistsNoBatchAndNoLedgerDraft_YetTheReceiptIsSaved()
    {
        var rig = new Rig();
        var sku = rig.Skus.Add();

        var response = await rig.Handler().HandleAsync(rig.Command(rig.Line(sku, accepted: 0m, rejected: 4m, reason: "WrongItem")));

        Assert.True(response.IsSuccess);
        Assert.Empty(rig.Batches.Enlisted);
        Assert.Empty(rig.Ledger.Appended);
        Assert.Single(rig.Receipts.Saved);
    }

    [Fact]
    public async Task AnyViolation_SavesNothingAndEnlistsNothing_AndEveryViolationIsReported()
    {
        var rig = new Rig();
        var food = rig.Skus.Add(expiryRequired: true, stockClass: StockClass.Food);
        var litter = rig.Skus.Add();

        var response = await rig.Handler().HandleAsync(rig.Command(rig.Line(food), rig.Line(litter, accepted: 5m, rejected: 1m)));

        Assert.False(response.IsSuccess);
        Assert.Equal(2, response.ValidationResult!.Errors.Count());
        Assert.Contains(response.ValidationResult.Errors, e => e.ErrorCode == BusinessViolationCodes.ExpiryDateRequired && e.PropertyName == "Lines[0].ExpiresOn");
        Assert.Contains(response.ValidationResult.Errors, e => e.ErrorCode == BusinessViolationCodes.RejectionReasonRequired && e.PropertyName == "Lines[1].RejectionReason");
        Assert.Empty(rig.Receipts.Saved);
        Assert.Empty(rig.Batches.Enlisted);
        Assert.Empty(rig.Ledger.Appended);
    }

    [Fact]
    public async Task AHeldLineMakesAQuarantinedBatch()
    {
        var rig = new Rig();
        var sku = rig.Skus.Add();

        await rig.Handler().HandleAsync(rig.Command(rig.Line(sku, hold: true)));

        Assert.Equal(BatchStatus.Quarantined, Assert.Single(rig.Batches.Enlisted).Status);
    }

    [Fact]
    public async Task AWriterRefusal_SavesNothing()
    {
        var rig = new Rig();
        var sku = rig.Skus.Add();
        rig.Ledger.Refusal = Domain.Models.StockResult<CatEcommerce.Shared.Common.Helpers.Unit>.Failure(BusinessViolationCodes.StockLocationNotFound, "No such location.", "Drafts[0].LocationId");

        var response = await rig.Handler().HandleAsync(rig.Command(rig.Line(sku)));

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.StockLocationNotFound, Assert.Single(response.ValidationResult!.Errors).ErrorCode);
        Assert.Empty(rig.Receipts.Saved);
    }

    [Fact]
    public async Task APurchaseOrderThatIsNotIssuedOrIsAnotherBranchs_IsRefusedBeforeAnyLineIsRead()
    {
        var rig = new Rig();
        var sku = rig.Skus.Add();
        var draft = new PurchaseOrderAggregate(Guid.NewGuid());
        draft.CreateDraft(new PurchaseOrderDraftDto("PO-1", Guid.NewGuid(), Branch, rig.Warehouse.LocationId, "BDT", Today.AddDays(5), null,
            [new PurchaseOrderLineDto(Guid.NewGuid(), sku.SkuId, sku.Snapshot, false, 10m, 5m)], new InventoryActor(TestUserContext.Create(), Guid.NewGuid())));
        rig.Orders.Seed(draft);

        var notIssued = rig.Command(rig.Line(sku)) ;
        var response = await rig.Handler().HandleAsync(new PostGoodsReceiptCommand
        {
            GoodsReceiptId = Guid.NewGuid(), BranchId = Branch, PurchaseOrderId = draft.Id, Lines = notIssued.Lines, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.PurchaseOrderNotIssued);

        var foreign = await rig.Handler().HandleAsync(new PostGoodsReceiptCommand
        {
            GoodsReceiptId = Guid.NewGuid(), BranchId = Guid.NewGuid(), PurchaseOrderId = draft.Id, Lines = notIssued.Lines, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Contains(foreign.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.PurchaseOrderNotFound);
        Assert.Empty(rig.Receipts.Saved);
    }

    // ── write-off ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static BatchAggregate Batch(Guid sku)
    {
        var batch = new BatchAggregate(Guid.NewGuid());
        batch.Create(new NewBatchDto(batch.Id, Branch, Guid.NewGuid(), Guid.NewGuid(), null, null, sku, new SkuSnapshot("SKU-1", "Product", "each", StockClass.General), "LOT-9",
            null, null, null, DateTimeOffset.UtcNow, 10m, 5m, "BDT", 5m, BatchStatus.Available, new InventoryActor(TestUserContext.Create(), Guid.NewGuid())));
        return batch;
    }

    [Fact]
    public async Task WriteOff_EnlistsOneWastageDraftPerPositionWithStock_ThenSavesTheBatchOnce()
    {
        var positions = new FakeStockPositionReader();
        var shop = positions.AddLocation(Branch, StockLocationKind.ShopFloor);
        var warehouse = positions.AddLocation(Branch, StockLocationKind.Warehouse);
        var sku = Guid.NewGuid();
        var batch = Batch(sku);
        positions.BatchPositions.Add(new StockPositionRow(sku, batch.Id, shop.LocationId, 4m, 0m));
        positions.BatchPositions.Add(new StockPositionRow(sku, batch.Id, warehouse.LocationId, 6m, 0m));
        var repository = new FakeAggregateRootRepository<BatchAggregate>();
        repository.Seed(batch);
        var ledger = new FakeLedgerWriter();

        var response = await new WriteOffBatchCommandHandler(NullLogger<WriteOffBatchCommandHandler>.Instance, repository, positions, ledger, new FakeAggregateRootRepository<Domain.Aggregates.Administration.StaffBranchAssignmentAggregate>(), TimeProvider.System)
            .HandleAsync(new WriteOffBatchCommand { BatchId = batch.Id, BranchId = Branch, Reason = "Water damage.", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        Assert.Equal(BatchStatus.WrittenOff, batch.Status);
        Assert.Equal([-6m, -4m], ledger.Appended.Select(d => d.QuantityDelta).Order());
        Assert.All(ledger.Appended, d => { Assert.Equal(MovementKind.Wastage, d.Kind); Assert.Equal(StockReferenceKind.BatchWriteOff, d.ReferenceKind); Assert.Equal(batch.Id, d.ReferenceId); });
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task WriteOff_RefusesWhileOrdersHoldReservationsOnTheBatch()
    {
        var positions = new FakeStockPositionReader();
        var shop = positions.AddLocation(Branch, StockLocationKind.ShopFloor);
        var batch = Batch(Guid.NewGuid());
        positions.BatchPositions.Add(new StockPositionRow(batch.SkuId, batch.Id, shop.LocationId, 10m, 3m));
        var repository = new FakeAggregateRootRepository<BatchAggregate>();
        repository.Seed(batch);
        var ledger = new FakeLedgerWriter();

        var response = await new WriteOffBatchCommandHandler(NullLogger<WriteOffBatchCommandHandler>.Instance, repository, positions, ledger, new FakeAggregateRootRepository<Domain.Aggregates.Administration.StaffBranchAssignmentAggregate>(), TimeProvider.System)
            .HandleAsync(new WriteOffBatchCommand { BatchId = batch.Id, BranchId = Branch, Reason = "Water damage.", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(BusinessViolationCodes.InsufficientStock, Assert.Single(response.ValidationResult!.Errors).ErrorCode);
        Assert.Equal(BatchStatus.Available, batch.Status);
        Assert.Empty(ledger.Appended);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task WriteOff_OfABatchAlsoHeldAtAnotherBranch_NeedsTheCallerAssignedToThatBranch()
    {
        var otherBranch = Guid.NewGuid();
        var caller = TestUserContext.Create();

        async Task<(Platform.Infrastructure.Core.Commands.CommandResponse Response, BatchAggregate Batch, FakeLedgerWriter Ledger)> RunAsync(bool assigned)
        {
            var positions = new FakeStockPositionReader();
            var here = positions.AddLocation(Branch, StockLocationKind.Warehouse);
            var there = positions.AddLocation(otherBranch, StockLocationKind.Warehouse);
            var batch = Batch(Guid.NewGuid());
            positions.BatchPositions.Add(new StockPositionRow(batch.SkuId, batch.Id, here.LocationId, 4m, 0m));
            positions.BatchPositions.Add(new StockPositionRow(batch.SkuId, batch.Id, there.LocationId, 6m, 0m));
            var repository = new FakeAggregateRootRepository<BatchAggregate>();
            repository.Seed(batch);
            var assignments = new FakeAggregateRootRepository<Domain.Aggregates.Administration.StaffBranchAssignmentAggregate>();
            if (assigned)
            {
                var assignment = new Domain.Aggregates.Administration.StaffBranchAssignmentAggregate(caller.UserId);
                assignment.Assign(new Domain.Models.StaffBranchAssignDto([Branch, otherBranch], Branch, caller, Guid.NewGuid()));
                assignments.Seed(assignment);
            }

            var ledger = new FakeLedgerWriter();
            var response = await new WriteOffBatchCommandHandler(NullLogger<WriteOffBatchCommandHandler>.Instance, repository, positions, ledger, assignments, TimeProvider.System)
                .HandleAsync(new WriteOffBatchCommand { BatchId = batch.Id, BranchId = Branch, Reason = "Water damage.", UserContext = caller, CorrelationId = Guid.NewGuid() });
            return (response, batch, ledger);
        }

        var refused = await RunAsync(assigned: false);
        Assert.False(refused.Response.IsSuccess);
        Assert.Equal(BatchStatus.Available, refused.Batch.Status);
        Assert.Empty(refused.Ledger.Appended);

        var allowed = await RunAsync(assigned: true);
        Assert.True(allowed.Response.IsSuccess);
        Assert.Equal(2, allowed.Ledger.Appended.Count);
    }

    [Fact]
    public async Task BatchCommands_OnAnotherBranchsBatch_AreNotFound()
    {
        var batch = Batch(Guid.NewGuid());
        var repository = new FakeAggregateRootRepository<BatchAggregate>();
        repository.Seed(batch);

        var response = await new QuarantineBatchCommandHandler(NullLogger<QuarantineBatchCommandHandler>.Instance, repository, TimeProvider.System)
            .HandleAsync(new QuarantineBatchCommand { BatchId = batch.Id, BranchId = Guid.NewGuid(), Reason = "Torn.", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(InventoryRules.NotFoundResource, Assert.Single(response.ValidationResult!.Errors).ResourceName);
        Assert.Equal(BatchStatus.Available, batch.Status);
    }
}
