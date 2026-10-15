using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.10 (US-IMP-13 AC2, ADR-107). A mandatory reason CATEGORY and a mandatory NOTE (the why, not only a category).
/// Worth at most the configured threshold: <c>Posted</c> at once. Worth more: <c>PendingApproval</c> with NO ledger entry until a
/// DIFFERENT approver holding <c>Stock.ApproveAdjustment</c> approves. Whether to write entries is a decision this aggregate makes
/// (<see cref="BuildDrafts"/> returns none unless <c>Posted</c>); writing them is the handler's, in the same commit.
/// </summary>
public sealed class StockAdjustmentAggregate : AggregateRoot
{
    private readonly List<StockAdjustmentLine> _lines = [];

    public string Number { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public AdjustmentReason Reason { get; private set; } = default!;
    public string Note { get; private set; } = default!;
    public AdjustmentStatus Status { get; private set; } = AdjustmentStatus.PendingApproval;
    public decimal ValueBase { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public string? DecisionNote { get; private set; }
    public IReadOnlyList<StockAdjustmentLine> Lines => _lines;

    private StockAdjustmentAggregate() { }

    public StockAdjustmentAggregate(Guid id) : base(id) { }

    public void Request(StockAdjustmentRequestDto dto)
    {
        if (dto.Reason is null)
            throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentReasonRequired, "A reason is required for every stock adjustment.", nameof(Reason));
        var note = InventoryViolation.Normalise(dto.Note);
        if (note.Length < InventoryRules.ReasonMinLength || note.Length > InventoryRules.NoteMaxLength)
            throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentNoteRequired, "Explain the adjustment in 5 to 500 characters.", nameof(Note));
        if (dto.Lines.Count == 0 || dto.Lines.Count > InventoryRules.MaxAdjustmentLines)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, $"An adjustment has between 1 and {InventoryRules.MaxAdjustmentLines} lines.", nameof(Lines));
        if (dto.Lines.Select(l => l.BatchId).Distinct().Count() != dto.Lines.Count)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "A batch can appear on an adjustment only once.", nameof(Lines));

        var reason = dto.Reason;
        for (var i = 0; i < dto.Lines.Count; i++)
        {
            var line = dto.Lines[i];
            var property = $"Lines[{i}].QuantityDelta";
            if (line.QuantityDelta == 0m || decimal.Round(line.QuantityDelta, 3) != line.QuantityDelta)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "A quantity must be non-zero, with at most three decimal places.", property);

            // The wrong sign for the reason -- only Found and Correction may go either way.
            var mustBeNegative = reason != AdjustmentReason.Found && reason != AdjustmentReason.Correction;
            if (mustBeNegative && line.QuantityDelta > 0m)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidAdjustmentDirection, $"A {reason.DisplayName} adjustment can only reduce stock.", property);
            if (reason == AdjustmentReason.Found && line.QuantityDelta < 0m)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidAdjustmentDirection, "Found stock can only increase stock.", property);

            if (reason == AdjustmentReason.Expired && !line.BatchIsExpired)
                throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentReasonNotApplicable, "That batch has not expired.", $"Lines[{i}].BatchId");
            if (reason == AdjustmentReason.RecallDisposal && line.BatchStatus != BatchStatus.Recalled)
                throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentReasonNotApplicable, "That batch is not under recall.", $"Lines[{i}].BatchId");
        }

        _lines.Clear();
        _lines.AddRange(dto.Lines.Select(l => new StockAdjustmentLine(l.LineId, l.SkuId, l.BatchId, l.QuantityDelta, l.UnitCostBase)));

        Number = dto.Number;
        BranchId = dto.BranchId;
        LocationId = dto.LocationId;
        Reason = reason;
        Note = note;
        CurrencyCode = dto.BaseCurrency;
        ValueBase = new Money(dto.Lines.Sum(l => Math.Abs(l.QuantityDelta) * l.UnitCostBase), dto.BaseCurrency).Amount;
        RequestedBy = dto.Actor.UserContext.UserId;
        RequestedOn = dto.RequestedOn;
        Status = ValueBase <= dto.ApprovalThreshold ? AdjustmentStatus.Posted : AdjustmentStatus.PendingApproval;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StockAdjustmentRequestedEvent { AdjustmentId = Id, Status = Status, ValueBase = ValueBase, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>A different person must approve (<c>ApproverMustDiffer</c>); posts the entries then.</summary>
    public void Approve(Guid approverId, DateTimeOffset on, string? note, InventoryActor actor)
    {
        RequirePending();
        if (approverId == RequestedBy)
            throw InventoryViolation.Of(BusinessViolationCodes.ApproverMustDiffer, "An adjustment must be approved by someone other than the person who requested it.", nameof(approverId));

        Decide(AdjustmentStatus.Posted, approverId, on, note, actor);
    }

    public void Reject(Guid approverId, DateTimeOffset on, string? note, InventoryActor actor)
    {
        RequirePending();
        Decide(AdjustmentStatus.Rejected, approverId, on, note, actor);
    }

    /// <summary>Only the requester may withdraw.</summary>
    public void Withdraw(Guid by, DateTimeOffset on, InventoryActor actor)
    {
        RequirePending();
        if (by != RequestedBy)
            throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentWithdrawNotRequester, "Only the person who requested an adjustment can withdraw it.", nameof(by));

        Decide(AdjustmentStatus.Withdrawn, by, on, null, actor);
    }

    /// <summary>The ledger entries for a POSTED adjustment; none while pending, rejected or withdrawn (U-F2).</summary>
    public IReadOnlyList<StockMovementDraft> BuildDrafts() =>
        Status != AdjustmentStatus.Posted
            ? []
            : _lines.Select(l => new StockMovementDraft(
                BranchId, LocationId, l.SkuId, l.BatchId, MovementKindRules.ForAdjustment(Reason, l.QuantityDelta), l.QuantityDelta,
                StockReferenceKind.StockAdjustment, Id, l.Id, Number, $"{Reason.DisplayName}: {Note}")).ToList();

    private void Decide(AdjustmentStatus to, Guid by, DateTimeOffset on, string? note, InventoryActor actor)
    {
        Status = to;
        DecidedBy = by;
        DecidedOn = on;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        AddDomainEvent(new StockAdjustmentDecidedEvent { AdjustmentId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void RequirePending()
    {
        if (Status != AdjustmentStatus.PendingApproval)
            throw InventoryViolation.Of(BusinessViolationCodes.AdjustmentNotPending, "This adjustment is not waiting for approval.", nameof(Status));
    }
}
