using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase F -- U-F1 ... U-F8: adjustments (reason, note, direction, approval), counts (blind, frozen variance, exact commit) and
/// transfers.</summary>
public sealed class StockControlTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Location = Guid.NewGuid();

    private static InventoryActor Actor(Guid? userId = null) => new(new Platform.Infrastructure.Common.Security.UserContext(userId ?? Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());

    // ── adjustments ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static AdjustmentLineDto AdjLine(decimal delta, decimal unitCost = 10m, bool expired = false, BatchStatus? status = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), delta, expired, status ?? BatchStatus.Available, unitCost);

    private static StockAdjustmentRequestDto Request(
        AdjustmentReason? reason, string? note, IReadOnlyList<AdjustmentLineDto> lines, InventoryActor? actor = null, decimal threshold = 5000m) =>
        new("ADJ-2026-000001", Branch, Location, reason, note, threshold, "BDT", Now, lines, actor ?? Actor());

    private static StockAdjustmentAggregate Requested(AdjustmentReason reason, params AdjustmentLineDto[] lines)
    {
        var adjustment = new StockAdjustmentAggregate(Guid.NewGuid());
        adjustment.Request(Request(reason, "Counted wrongly last week.", lines));
        return adjustment;
    }

    [Fact]
    public void U_F1_AnAdjustmentWithoutAReason_OrWithoutANote_IsRefused()
    {
        var lines = new[] { AdjLine(-1m) };

        Assert.Equal(BusinessViolationCodes.AdjustmentReasonRequired,
            Assert.Throws<DomainRuleViolationException>(() => new StockAdjustmentAggregate(Guid.NewGuid()).Request(Request(null, "A note.", lines))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.AdjustmentNoteRequired,
            Assert.Throws<DomainRuleViolationException>(() => new StockAdjustmentAggregate(Guid.NewGuid()).Request(Request(AdjustmentReason.Damage, null, lines))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.AdjustmentNoteRequired,
            Assert.Throws<DomainRuleViolationException>(() => new StockAdjustmentAggregate(Guid.NewGuid()).Request(Request(AdjustmentReason.Damage, "no", lines))).ErrorCode);
    }

    [Fact]
    public void U_F1_FoundCannotBeNegative_AndTheftCannotBePositive()
    {
        Assert.Equal(BusinessViolationCodes.InvalidAdjustmentDirection,
            Assert.Throws<DomainRuleViolationException>(() => new StockAdjustmentAggregate(Guid.NewGuid()).Request(Request(AdjustmentReason.Found, "Found behind a shelf.", [AdjLine(-2m)]))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAdjustmentDirection,
            Assert.Throws<DomainRuleViolationException>(() => new StockAdjustmentAggregate(Guid.NewGuid()).Request(Request(AdjustmentReason.Theft, "Reported to the police.", [AdjLine(2m)]))).ErrorCode);
    }

    [Theory]
    [InlineData("Damage", true)]
    [InlineData("Theft", true)]
    [InlineData("Sampling", true)]
    [InlineData("InternalUse", true)]
    [InlineData("Found", false)]
    [InlineData("Correction", false)]
    public void U_F1_EveryReasonAndTheDirectionsItAllows(string reasonName, bool outOnly)
    {
        var reason = Enumeration.FromDisplayName<AdjustmentReason>(reasonName);

        if (reasonName == "Found")
            Assert.Throws<DomainRuleViolationException>(() => Requested(reason, AdjLine(-3m)));
        else
            Assert.Single(Requested(reason, AdjLine(-3m)).BuildDrafts());

        if (outOnly)
            Assert.Throws<DomainRuleViolationException>(() => Requested(reason, AdjLine(3m)));
        else
            Assert.Single(Requested(reason, AdjLine(3m)).BuildDrafts());
    }

    [Fact]
    public void U_F1_TheDraftKindFollowsTheReason_WastageForExpiredAndRecallDisposal_AdjustmentInOutOtherwise()
    {
        var damage = Requested(AdjustmentReason.Damage, AdjLine(-1m)).BuildDrafts().Single();
        var found = Requested(AdjustmentReason.Found, AdjLine(1m)).BuildDrafts().Single();
        var expired = Requested(AdjustmentReason.Expired, AdjLine(-1m, expired: true)).BuildDrafts().Single();
        var recall = Requested(AdjustmentReason.RecallDisposal, AdjLine(-1m, status: BatchStatus.Recalled)).BuildDrafts().Single();

        Assert.Equal(MovementKind.AdjustmentOut, damage.Kind);
        Assert.Equal(MovementKind.AdjustmentIn, found.Kind);
        Assert.Equal(MovementKind.Wastage, expired.Kind);
        Assert.Equal(MovementKind.Wastage, recall.Kind);
        Assert.All(new[] { damage, found, expired, recall }, d => Assert.Equal(StockReferenceKind.StockAdjustment, d.ReferenceKind));
    }

    // ── U-F2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F2_ValueJustUnderTheThreshold_IsPostedAtOnceWithItsDrafts()
    {
        var adjustment = new StockAdjustmentAggregate(Guid.NewGuid());

        adjustment.Request(Request(AdjustmentReason.Damage, "Pest damage.", [AdjLine(-1m, unitCost: 4999.99m)]));

        Assert.Equal(4999.99m, adjustment.ValueBase);
        Assert.Equal(AdjustmentStatus.Posted, adjustment.Status);
        Assert.Single(adjustment.BuildDrafts());
    }

    [Fact]
    public void U_F2_ValueJustOverTheThreshold_IsPendingApprovalAndWritesNoDraft()
    {
        var adjustment = new StockAdjustmentAggregate(Guid.NewGuid());

        adjustment.Request(Request(AdjustmentReason.Damage, "Pest damage.", [AdjLine(-1m, unitCost: 5000.01m)]));

        Assert.Equal(5000.01m, adjustment.ValueBase);
        Assert.Equal(AdjustmentStatus.PendingApproval, adjustment.Status);
        Assert.Empty(adjustment.BuildDrafts());
    }

    [Fact]
    public void U_F2_ValueExactlyAtTheThreshold_IsPosted_AndTheValueSumsAbsoluteDeltasTimesCost()
    {
        var adjustment = new StockAdjustmentAggregate(Guid.NewGuid());

        adjustment.Request(Request(AdjustmentReason.Correction, "Recount.", [AdjLine(-100m, 25m), AdjLine(40m, 62.5m)]));

        Assert.Equal(5000.00m, adjustment.ValueBase); // |-100| x 25 + |40| x 62.5
        Assert.Equal(AdjustmentStatus.Posted, adjustment.Status);
        Assert.Equal("BDT", adjustment.CurrencyCode);
    }

    // ── U-F3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static StockAdjustmentAggregate Pending(Guid requester)
    {
        var adjustment = new StockAdjustmentAggregate(Guid.NewGuid());
        adjustment.Request(Request(AdjustmentReason.Damage, "Pest damage.", [AdjLine(-1m, unitCost: 9000m)], Actor(requester)));
        return adjustment;
    }

    [Fact]
    public void U_F3_TheRequesterCannotApproveTheirOwnAdjustment()
    {
        var requester = Guid.NewGuid();
        var adjustment = Pending(requester);

        var ex = Assert.Throws<DomainRuleViolationException>(() => adjustment.Approve(requester, Now, null, Actor(requester)));

        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, ex.ErrorCode);
        Assert.Equal(AdjustmentStatus.PendingApproval, adjustment.Status);
    }

    [Fact]
    public void U_F3_AnotherApproverPostsIt_AndOnlyThenDoTheDraftsExist()
    {
        var adjustment = Pending(Guid.NewGuid());
        Assert.Empty(adjustment.BuildDrafts());

        var approver = Guid.NewGuid();
        adjustment.Approve(approver, Now, "Checked the shelf.", Actor(approver));

        Assert.Equal(AdjustmentStatus.Posted, adjustment.Status);
        Assert.Equal(approver, adjustment.DecidedBy);
        Assert.Single(adjustment.BuildDrafts());
        Assert.Equal(BusinessViolationCodes.AdjustmentNotPending,
            Assert.Throws<DomainRuleViolationException>(() => adjustment.Approve(Guid.NewGuid(), Now, null, Actor())).ErrorCode);
    }

    [Fact]
    public void U_F3_RejectAndWithdrawEndItWithNoDraft_AndOnlyTheRequesterMayWithdraw()
    {
        var requester = Guid.NewGuid();

        var rejected = Pending(requester);
        rejected.Reject(Guid.NewGuid(), Now, "Not justified.", Actor());
        Assert.Equal(AdjustmentStatus.Rejected, rejected.Status);
        Assert.Empty(rejected.BuildDrafts());

        var withdrawn = Pending(requester);
        Assert.Equal(BusinessViolationCodes.AdjustmentWithdrawNotRequester,
            Assert.Throws<DomainRuleViolationException>(() => withdrawn.Withdraw(Guid.NewGuid(), Now, Actor())).ErrorCode);
        withdrawn.Withdraw(requester, Now, Actor(requester));
        Assert.Equal(AdjustmentStatus.Withdrawn, withdrawn.Status);
        Assert.Empty(withdrawn.BuildDrafts());
    }

    // ── U-F4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F4_TheExpiredReasonNeedsAnExpiredBatch_AndRecallDisposalNeedsARecalledOne()
    {
        Assert.Equal(BusinessViolationCodes.AdjustmentReasonNotApplicable,
            Assert.Throws<DomainRuleViolationException>(() => Requested(AdjustmentReason.Expired, AdjLine(-1m, expired: false))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.AdjustmentReasonNotApplicable,
            Assert.Throws<DomainRuleViolationException>(() => Requested(AdjustmentReason.RecallDisposal, AdjLine(-1m, status: BatchStatus.Available))).ErrorCode);

        Assert.NotNull(Requested(AdjustmentReason.Expired, AdjLine(-1m, expired: true)));
        Assert.NotNull(Requested(AdjustmentReason.RecallDisposal, AdjLine(-1m, status: BatchStatus.Recalled)));
    }

    // ── counts ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static StockCountAggregate OpenCount(StockCountScope? scope = null, params Guid[] skus)
    {
        var count = new StockCountAggregate(Guid.NewGuid());
        count.Open(new StockCountOpenDto("CNT-2026-000001", Branch, Location, scope ?? StockCountScope.FullLocation, skus, Now, Actor()));
        return count;
    }

    [Fact]
    public void U_F5_CountingIsBlind_WhileOpen_AndOnlyThenAreBookQuantitiesHidden()
    {
        Assert.False(StockCountAggregate.BookQuantitiesVisible(StockCountStatus.Open));
        Assert.All(
            new[] { StockCountStatus.Submitted, StockCountStatus.AwaitingApproval, StockCountStatus.Committed, StockCountStatus.Cancelled },
            status => Assert.True(StockCountAggregate.BookQuantitiesVisible(status)));
    }

    [Fact]
    public void U_F5_ACountLineHasNoBookQuantityUntilItIsSubmitted()
    {
        var count = OpenCount();
        var sku = Guid.NewGuid();
        var batch = Guid.NewGuid();

        count.RecordLines([new CountLineDto(sku, batch, 4m)], Now, Actor());

        var line = Assert.Single(count.Lines);
        Assert.Null(line.BookQuantity);
        Assert.Null(line.Variance);
    }

    [Fact]
    public void U_F6_AFullLocationSubmitTurnsAnUncountedPositionOf7IntoALineCountedZeroWithVariableMinus7()
    {
        var count = OpenCount();
        var countedSku = Guid.NewGuid();
        var countedBatch = Guid.NewGuid();
        var missedSku = Guid.NewGuid();
        var missedBatch = Guid.NewGuid();
        count.RecordLines([new CountLineDto(countedSku, countedBatch, 8m)], Now, Actor());

        count.Submit(new StockCountSubmitDto(
            [new CountBookDto(countedSku, countedBatch, 10m, 5m), new CountBookDto(missedSku, missedBatch, 7m, 2m)], IsFullLocation: true, "BDT", Now, Actor()));

        Assert.Equal(StockCountStatus.Submitted, count.Status);
        var counted = count.Lines.Single(l => l.BatchId == countedBatch);
        Assert.Equal((10m, -2m), (counted.BookQuantity, counted.Variance));
        var missed = count.Lines.Single(l => l.BatchId == missedBatch);
        Assert.Equal(0m, missed.CountedQuantity);
        Assert.Equal(7m, missed.BookQuantity);
        Assert.Equal(-7m, missed.Variance);
        Assert.Equal(2 * 5m + 7 * 2m, count.VarianceValueBase); // |-2| x 5 + |-7| x 2
        Assert.Empty(count.BuildDrafts()); // NOTHING reaches the ledger at submit (US-IMP-14 AC1)
    }

    [Fact]
    public void U_F6_ASelectedSkuSubmitDoesNotInventLinesForUncountedPositions()
    {
        var sku = Guid.NewGuid();
        var count = OpenCount(StockCountScope.SelectedSkus, sku);
        var counted = Guid.NewGuid();
        count.RecordLines([new CountLineDto(sku, counted, 3m)], Now, Actor());

        count.Submit(new StockCountSubmitDto([new CountBookDto(sku, counted, 3m, 1m), new CountBookDto(sku, Guid.NewGuid(), 9m, 1m)], IsFullLocation: false, "BDT", Now, Actor()));

        Assert.Single(count.Lines);
        Assert.Equal(0m, count.VarianceValueBase);
    }

    [Fact]
    public void U_F7_CommitPostsExactlyTheSubmittedVariances_AsCountGainAndCountLoss_ReferencingTheCount()
    {
        var count = OpenCount();
        var (skuA, batchA, skuB, batchB, skuC, batchC) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        count.RecordLines([new CountLineDto(skuA, batchA, 12m), new CountLineDto(skuB, batchB, 5m), new CountLineDto(skuC, batchC, 3m)], Now, Actor());
        count.Submit(new StockCountSubmitDto(
            [new CountBookDto(skuA, batchA, 10m, 1m), new CountBookDto(skuB, batchB, 5m, 1m), new CountBookDto(skuC, batchC, 4m, 1m)], true, "BDT", Now, Actor()));

        count.Commit(5000m, Now, Actor());
        var drafts = count.BuildDrafts();

        Assert.Equal(StockCountStatus.Committed, count.Status);
        Assert.Equal(2, drafts.Count); // the matching line posts nothing
        var gain = drafts.Single(d => d.BatchId == batchA);
        var loss = drafts.Single(d => d.BatchId == batchC);
        Assert.Equal((MovementKind.CountGain, 2m), (gain.Kind, gain.QuantityDelta));
        Assert.Equal((MovementKind.CountLoss, -1m), (loss.Kind, loss.QuantityDelta));
        Assert.All(drafts, d => { Assert.Equal(StockReferenceKind.StockCount, d.ReferenceKind); Assert.Equal(count.Id, d.ReferenceId); Assert.Equal(count.Number, d.ReferenceLabel); });
    }

    [Fact]
    public void U_F7_AVarianceOverTheThresholdWaitsForADifferentApprover_AndRejectionReturnsItToSubmitted()
    {
        var committer = Guid.NewGuid();
        var count = OpenCount();
        var (sku, batch) = (Guid.NewGuid(), Guid.NewGuid());
        count.RecordLines([new CountLineDto(sku, batch, 0m)], Now, Actor());
        count.Submit(new StockCountSubmitDto([new CountBookDto(sku, batch, 100m, 100m)], true, "BDT", Now, Actor()));

        count.Commit(5000m, Now, Actor(committer));
        Assert.Equal(StockCountStatus.AwaitingApproval, count.Status);
        Assert.Empty(count.BuildDrafts());
        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, Assert.Throws<DomainRuleViolationException>(() => count.Approve(committer, Now, Actor(committer))).ErrorCode);

        count.Reject(Guid.NewGuid(), Actor());
        Assert.Equal(StockCountStatus.Submitted, count.Status);

        count.Commit(5000m, Now, Actor(committer));
        var approver = Guid.NewGuid();
        count.Approve(approver, Now, Actor(approver));
        Assert.Equal(StockCountStatus.Committed, count.Status);
        Assert.Single(count.BuildDrafts());
    }

    [Theory]
    [InlineData("Open", "record,submit,cancel")]
    [InlineData("Submitted", "commit,cancel")]
    public void U_F7_TheCountTransitionsOfSection8_10(string from, string allowed)
    {
        var (sku, batch) = (Guid.NewGuid(), Guid.NewGuid());
        StockCountAggregate Build()
        {
            var count = OpenCount();
            count.RecordLines([new CountLineDto(sku, batch, 1m)], Now, Actor());
            if (from == "Submitted") count.Submit(new StockCountSubmitDto([new CountBookDto(sku, batch, 1m, 1m)], true, "BDT", Now, Actor()));
            return count;
        }

        var actions = allowed.Split(',');
        Assert.Equal(actions.Contains("record"), Try(() => Build().RecordLines([new CountLineDto(sku, batch, 2m)], Now, Actor())));
        Assert.Equal(actions.Contains("submit"), Try(() => Build().Submit(new StockCountSubmitDto([new CountBookDto(sku, batch, 1m, 1m)], true, "BDT", Now, Actor()))));
        Assert.Equal(actions.Contains("commit"), Try(() => Build().Commit(5000m, Now, Actor())));
        Assert.Equal(actions.Contains("cancel"), Try(() => Build().Cancel(Actor())));
        Assert.False(Try(() => Build().Approve(Guid.NewGuid(), Now, Actor())));

        static bool Try(Action action)
        {
            try { action(); return true; }
            catch (DomainRuleViolationException) { return false; }
        }
    }

    [Fact]
    public void U_F7_ALineOutsideASelectedScopeIsRefused_AndOnlyOpenCountsTakeLines()
    {
        var inScope = Guid.NewGuid();
        var count = OpenCount(StockCountScope.SelectedSkus, inScope);

        Assert.Equal(BusinessViolationCodes.CountLineOutsideScope,
            Assert.Throws<DomainRuleViolationException>(() => count.RecordLines([new CountLineDto(Guid.NewGuid(), Guid.NewGuid(), 1m)], Now, Actor())).ErrorCode);
        count.RecordLines([new CountLineDto(inScope, Guid.NewGuid(), 1m)], Now, Actor());
        Assert.Throws<DomainRuleViolationException>(() => new StockCountAggregate(Guid.NewGuid()).Open(
            new StockCountOpenDto("CNT-1", Branch, Location, StockCountScope.SelectedSkus, [], Now, Actor())));
    }

    // ── transfers ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static StockTransferPostDto Transfer(Guid from, Guid to, params StockTransferLineDto[] lines) =>
        new("TRF-2026-000001", Branch, from, to, "Restocking the shop floor.", Now, lines, Actor());

    [Fact]
    public void U_F8_ATransferToTheSameLocationIsRefused()
    {
        var location = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            new StockTransferAggregate(Guid.NewGuid()).Post(Transfer(location, location, new StockTransferLineDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5m))));

        Assert.Equal(BusinessViolationCodes.TransferLocationsMustDiffer, ex.ErrorCode);
    }

    [Fact]
    public void U_F8_ATransferWritesALedgerPairPerLine_OutAtTheSourceAndInAtTheDestination_WithOneReference()
    {
        var (from, to, toBranch) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (sku, batch) = (Guid.NewGuid(), Guid.NewGuid());
        var transfer = new StockTransferAggregate(Guid.NewGuid());
        transfer.Post(Transfer(from, to, new StockTransferLineDto(Guid.NewGuid(), sku, batch, 5m)));

        var drafts = transfer.BuildDrafts(toBranch);

        Assert.Equal(2, drafts.Count);
        var outEntry = drafts.Single(d => d.Kind == MovementKind.TransferOut);
        var inEntry = drafts.Single(d => d.Kind == MovementKind.TransferIn);
        Assert.Equal((from, Branch, -5m), (outEntry.LocationId, outEntry.BranchId, outEntry.QuantityDelta));
        Assert.Equal((to, toBranch, 5m), (inEntry.LocationId, inEntry.BranchId, inEntry.QuantityDelta));
        Assert.All(drafts, d => { Assert.Equal(StockReferenceKind.StockTransfer, d.ReferenceKind); Assert.Equal(transfer.Id, d.ReferenceId); Assert.Equal(batch, d.BatchId); });
    }

    [Fact]
    public void U_F8_ATransferNeedsBetween1And100DistinctBatchLinesWithPositiveQuantities()
    {
        var (from, to) = (Guid.NewGuid(), Guid.NewGuid());
        var batch = Guid.NewGuid();

        Assert.Throws<DomainRuleViolationException>(() => new StockTransferAggregate(Guid.NewGuid()).Post(Transfer(from, to)));
        Assert.Throws<DomainRuleViolationException>(() => new StockTransferAggregate(Guid.NewGuid()).Post(Transfer(from, to,
            new StockTransferLineDto(Guid.NewGuid(), Guid.NewGuid(), batch, 1m), new StockTransferLineDto(Guid.NewGuid(), Guid.NewGuid(), batch, 2m))));
        Assert.Throws<DomainRuleViolationException>(() => new StockTransferAggregate(Guid.NewGuid()).Post(Transfer(from, to, new StockTransferLineDto(Guid.NewGuid(), Guid.NewGuid(), batch, 0m))));
        Assert.Throws<DomainRuleViolationException>(() => new StockTransferAggregate(Guid.NewGuid()).Post(Transfer(from, to, new StockTransferLineDto(Guid.NewGuid(), Guid.NewGuid(), batch, 1.5555m))));
    }
}
