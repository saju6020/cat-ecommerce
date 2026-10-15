using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 §11.4 C10 and §11.2 -- the transfer handler's orchestration (what is read, enlisted and saved; nothing enlisted when refused) and
/// the shape validators of transfers, adjustments and counts.</summary>
public sealed class StockControlHandlerTests
{
    private static readonly Guid Branch = Guid.NewGuid();

    private sealed class Rig
    {
        public FakeAggregateRootRepository<StockTransferAggregate> Transfers { get; } = new();
        public FakeAggregateRootRepository<StaffBranchAssignmentAggregate> Assignments { get; } = new();
        public FakeSkuDirectory Skus { get; } = new();
        public FakeStockPositionReader Positions { get; } = new();
        public FakeLedgerWriter Ledger { get; } = new();
        public StockLocationFact From { get; }
        public StockLocationFact To { get; }
        public SkuStockProfile Sku { get; }
        public Guid BatchId { get; } = Guid.NewGuid();

        public Rig()
        {
            From = Positions.AddLocation(Branch, StockLocationKind.Warehouse);
            To = Positions.AddLocation(Branch, StockLocationKind.ShopFloor);
            Sku = Skus.Add();
            Positions.Batches[BatchId] = new BatchFact(BatchId, Sku.SkuId, Branch, "LOT-1", null, DateTimeOffset.UtcNow, BatchStatus.Available);
        }

        public PostStockTransferCommandHandler Handler() => new(
            NullLogger<PostStockTransferCommandHandler>.Instance, Transfers, Assignments, Skus, Positions, Ledger, new FakeDocumentNumbers(), TimeProvider.System);

        public PostStockTransferCommand Command(Guid? to = null, decimal quantity = 5m, Guid? batchId = null) => new()
        {
            TransferId = Guid.NewGuid(), BranchId = Branch, FromLocationId = From.LocationId, ToLocationId = to ?? To.LocationId,
            Lines = [new StockTransferLineInput(Guid.NewGuid(), Sku.SkuId, batchId ?? BatchId, quantity)],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };
    }

    private static string FirstCode(Platform.Infrastructure.Core.Commands.CommandResponse response) => response.ValidationResult!.Errors.First().ErrorCode;

    [Fact]
    public async Task APostedTransfer_EnlistsAnOutAndAnInPerLine_AndSavesOnce()
    {
        var rig = new Rig();

        var response = await rig.Handler().HandleAsync(rig.Command());

        Assert.True(response.IsSuccess);
        Assert.Single(rig.Transfers.Saved);
        Assert.Equal(2, rig.Ledger.Appended.Count);
        Assert.Contains(rig.Ledger.Appended, d => d.Kind == MovementKind.TransferOut && d.QuantityDelta == -5m);
        Assert.Contains(rig.Ledger.Appended, d => d.Kind == MovementKind.TransferIn && d.QuantityDelta == 5m);
    }

    [Fact]
    public async Task ATransferToTheSameLocation_OrAnUnknownBatch_OrAFractionOfAWholeItem_EnlistsAndSavesNothing()
    {
        var rig = new Rig();

        Assert.Equal(BusinessViolationCodes.TransferLocationsMustDiffer, FirstCode(await rig.Handler().HandleAsync(rig.Command(to: rig.From.LocationId))));
        Assert.Equal(BusinessViolationCodes.BatchNotFound, FirstCode(await rig.Handler().HandleAsync(rig.Command(batchId: Guid.NewGuid()))));
        Assert.False((await rig.Handler().HandleAsync(rig.Command(quantity: 1.5m))).IsSuccess);

        Assert.Empty(rig.Transfers.Saved);
        Assert.Empty(rig.Ledger.Appended);
    }

    [Fact]
    public async Task ADestinationAtAnotherBranch_NeedsTheCallerAssignedToIt_OtherwiseItDoesNotExistToThem()
    {
        var rig = new Rig();
        var elsewhere = rig.Positions.AddLocation(Guid.NewGuid(), StockLocationKind.ShopFloor);

        var response = await rig.Handler().HandleAsync(rig.Command(to: elsewhere.LocationId));

        Assert.Equal(BusinessViolationCodes.StockLocationNotFound, FirstCode(response));
        Assert.Empty(rig.Transfers.Saved);
    }

    // ── validators ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TransferValidator_NoLines_AndAZeroQuantity_Fail_AValidFractionPasses()
    {
        var rig = new Rig();
        var none = new PostStockTransferCommandValidator().Validate(new PostStockTransferCommand
        {
            TransferId = Guid.NewGuid(), BranchId = Branch, FromLocationId = rig.From.LocationId, ToLocationId = rig.To.LocationId, Lines = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        var zero = new PostStockTransferCommandValidator().Validate(rig.Command(quantity: 0m));
        var fine = new PostStockTransferCommandValidator().Validate(rig.Command(quantity: 2.5m));

        Assert.Contains(none.Errors, e => e.PropertyName == nameof(PostStockTransferCommand.Lines));
        Assert.Contains(zero.Errors, e => e.PropertyName.Contains("Quantity"));
        Assert.True(fine.IsValid);
    }

    [Fact]
    public void AdjustmentValidator_UnknownReason_FailsOnReason_ButAMissingReasonIsTheAggregatesRefusal()
    {
        var line = new AdjustmentLineInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m);
        RequestStockAdjustmentCommand Make(string? reason) => new()
        {
            AdjustmentId = Guid.NewGuid(), BranchId = Branch, LocationId = Guid.NewGuid(), Reason = reason, Note = "n", Lines = [line],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };

        Assert.Contains(new RequestStockAdjustmentCommandValidator().Validate(Make("Whim")).Errors, e => e.PropertyName == nameof(RequestStockAdjustmentCommand.Reason));
        Assert.True(new RequestStockAdjustmentCommandValidator().Validate(Make(null)).IsValid);
        Assert.True(new RequestStockAdjustmentCommandValidator().Validate(Make("damage")).IsValid);
    }

    [Fact]
    public void CountValidators_UnknownScope_NegativeCountedQuantity_AndARejectWithoutANote_Fail()
    {
        var open = new OpenStockCountCommandValidator().Validate(new OpenStockCountCommand
        { CountId = Guid.NewGuid(), BranchId = Branch, LocationId = Guid.NewGuid(), Scope = "Half", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        var lines = new RecordCountLinesCommandValidator().Validate(new RecordCountLinesCommand
        {
            CountId = Guid.NewGuid(), BranchId = Branch, Lines = [new CountLineInput(Guid.NewGuid(), Guid.NewGuid(), -1m)],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        var reject = new RejectStockAdjustmentCommandValidator().Validate(new RejectStockAdjustmentCommand
        { AdjustmentId = Guid.NewGuid(), BranchId = Branch, Note = "", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Contains(open.Errors, e => e.PropertyName == nameof(OpenStockCountCommand.Scope));
        Assert.Contains(lines.Errors, e => e.PropertyName.Contains("CountedQuantity"));
        Assert.Contains(reject.Errors, e => e.PropertyName == nameof(RejectStockAdjustmentCommand.Note));
    }
}
