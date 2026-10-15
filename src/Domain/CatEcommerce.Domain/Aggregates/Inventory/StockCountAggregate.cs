using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.10 (US-IMP-14, ADR-108). <c>Open -> Submitted -> Committed</c>; <c>Submitted -> AwaitingApproval -> Committed | Submitted
/// (rejected)</c>; <c>Open | Submitted -> Cancelled</c>. While <c>Open</c> a count is BLIND (OQ-13): counters never see book quantities
/// (the read side withholds them). <b>Submit</b> freezes each line's book quantity and variance and writes NOTHING to the ledger
/// (US-IMP-14 AC1). <b>Commit</b> posts EXACTLY the reported variances as <c>CountGain</c>/<c>CountLoss</c> -- not "set to counted" --
/// because a sale during counting is a real movement (ADR-108). A variance worth more than the threshold waits for a different
/// approver, like an adjustment (ADR-107).
/// </summary>
public sealed class StockCountAggregate : AggregateRoot
{
    private readonly List<StockCountLine> _lines = [];
    private readonly List<Guid> _skuIds = [];

    public string Number { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public StockCountScope Scope { get; private set; } = default!;
    public IReadOnlyList<Guid> SkuIds => _skuIds;
    public StockCountStatus Status { get; private set; } = StockCountStatus.Open;
    public decimal? VarianceValueBase { get; private set; }
    public string? CurrencyCode { get; private set; }
    public DateTimeOffset OpenedOn { get; private set; }
    public Guid OpenedBy { get; private set; }
    public DateTimeOffset? SubmittedOn { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public Guid? CommitRequestedBy { get; private set; }
    public DateTimeOffset? CommittedOn { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public IReadOnlyList<StockCountLine> Lines => _lines;

    /// <summary>The BLIND rule (OQ-13, US-IMP-14): while a count is <c>Open</c> its counters never see a book quantity or a variance. One pure rule
    /// the read side applies to every projection of a count's lines.</summary>
    public static bool BookQuantitiesVisible(StockCountStatus status) => status != StockCountStatus.Open;

    private StockCountAggregate() { }

    public StockCountAggregate(Guid id) : base(id) { }

    public void Open(StockCountOpenDto dto)
    {
        if (dto.Scope == StockCountScope.SelectedSkus && (dto.SkuIds.Count == 0 || dto.SkuIds.Count > InventoryRules.MaxCountSkus))
            throw InventoryViolation.Of(BusinessViolationCodes.CountLineOutsideScope, $"A selected-SKU count names between 1 and {InventoryRules.MaxCountSkus} SKUs.", nameof(SkuIds));

        Number = dto.Number;
        BranchId = dto.BranchId;
        LocationId = dto.LocationId;
        Scope = dto.Scope;
        _skuIds.Clear();
        if (dto.Scope == StockCountScope.SelectedSkus) _skuIds.AddRange(dto.SkuIds.Distinct());
        Status = StockCountStatus.Open;
        OpenedOn = dto.OpenedOn;
        OpenedBy = dto.Actor.UserContext.UserId;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StockCountOpenedEvent { CountId = Id, LocationId = LocationId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Counters record lines, upserting by (SKU, batch). <c>Open</c> only.</summary>
    public void RecordLines(IReadOnlyList<CountLineDto> lines, DateTimeOffset on, InventoryActor actor)
    {
        Require(StockCountStatus.Open, StockCountStatus.Open);
        if (lines.Count == 0 || _lines.Count + lines.Count > InventoryRules.MaxCountLines * 2)
            throw InventoryViolation.Of(BusinessViolationCodes.CountLineOutsideScope, "There are no lines, or too many.", nameof(lines));

        foreach (var (line, i) in lines.Select((l, i) => (l, i)))
        {
            if (line.CountedQuantity < 0m || decimal.Round(line.CountedQuantity, 3) != line.CountedQuantity)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "A counted quantity cannot be negative and has at most three decimal places.", $"Lines[{i}].CountedQuantity");
            if (Scope == StockCountScope.SelectedSkus && !_skuIds.Contains(line.SkuId))
                throw InventoryViolation.Of(BusinessViolationCodes.CountLineOutsideScope, "That SKU is not part of this count.", $"Lines[{i}].SkuId");

            var existing = _lines.FirstOrDefault(l => l.SkuId == line.SkuId && l.BatchId == line.BatchId);
            if (existing is null)
            {
                if (_lines.Count >= InventoryRules.MaxCountLines)
                    throw InventoryViolation.Of(BusinessViolationCodes.CountLineOutsideScope, $"A count holds at most {InventoryRules.MaxCountLines} lines.", nameof(Lines));
                _lines.Add(new StockCountLine(Guid.NewGuid(), line.SkuId, line.BatchId, line.CountedQuantity, actor.UserContext.UserId, on));
            }
            else existing.Recount(line.CountedQuantity, actor.UserContext.UserId, on);
        }

        Raise(actor);
    }

    /// <summary>Freezes the variance report. Nothing reaches the ledger (US-IMP-14 AC1). A full-location count turns every positive
    /// position that was not counted into a line counted 0.</summary>
    public void Submit(StockCountSubmitDto dto)
    {
        Require(StockCountStatus.Open, StockCountStatus.Submitted);

        var books = dto.Books.ToDictionary(b => (b.SkuId, b.BatchId));
        foreach (var line in _lines)
        {
            var book = books.GetValueOrDefault((line.SkuId, line.BatchId));
            line.Freeze(book?.BookQuantity ?? 0m, book?.UnitCostBase ?? 0m);
        }

        if (dto.IsFullLocation)
        {
            foreach (var book in dto.Books.Where(b => b.BookQuantity > 0m && !_lines.Any(l => l.SkuId == b.SkuId && l.BatchId == b.BatchId)))
            {
                var uncounted = new StockCountLine(Guid.NewGuid(), book.SkuId, book.BatchId, 0m, dto.Actor.UserContext.UserId, dto.SubmittedOn);
                uncounted.Freeze(book.BookQuantity, book.UnitCostBase);
                _lines.Add(uncounted);
            }
        }

        CurrencyCode = dto.BaseCurrency;
        VarianceValueBase = new Money(_lines.Sum(l => Math.Abs(l.Variance ?? 0m) * l.UnitCostBase), dto.BaseCurrency).Amount;
        Status = StockCountStatus.Submitted;
        SubmittedOn = dto.SubmittedOn;
        SubmittedBy = dto.Actor.UserContext.UserId;

        AddDomainEvent(new StockCountSubmittedEvent { CountId = Id, VarianceValueBase = VarianceValueBase.Value, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Commits at once, or waits for approval when the reported variance is worth more than the threshold.</summary>
    public void Commit(decimal approvalThreshold, DateTimeOffset on, InventoryActor actor)
    {
        Require(StockCountStatus.Submitted, StockCountStatus.Committed);

        CommitRequestedBy = actor.UserContext.UserId;
        if ((VarianceValueBase ?? 0m) > approvalThreshold)
        {
            Status = StockCountStatus.AwaitingApproval;
        }
        else
        {
            Status = StockCountStatus.Committed;
            CommittedOn = on;
        }

        AddDomainEvent(new StockCountCommittedEvent { CountId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>A different approver (like an adjustment, ADR-107).</summary>
    public void Approve(Guid approverId, DateTimeOffset on, InventoryActor actor)
    {
        Require(StockCountStatus.AwaitingApproval, StockCountStatus.Committed);
        if (approverId == CommitRequestedBy)
            throw InventoryViolation.Of(BusinessViolationCodes.ApproverMustDiffer, "A count must be approved by someone other than the person who committed it.", nameof(approverId));

        Status = StockCountStatus.Committed;
        CommittedOn = on;
        DecidedBy = approverId;

        AddDomainEvent(new StockCountCommittedEvent { CountId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>A rejected commit goes back to <c>Submitted</c> -- the variance report stays frozen.</summary>
    public void Reject(Guid approverId, InventoryActor actor)
    {
        Require(StockCountStatus.AwaitingApproval, StockCountStatus.Submitted);
        Status = StockCountStatus.Submitted;
        DecidedBy = approverId;
        CommitRequestedBy = null;
        Raise(actor);
    }

    public void Cancel(InventoryActor actor)
    {
        if (Status != StockCountStatus.Open && Status != StockCountStatus.Submitted)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCountTransition, $"A {Status.DisplayName} count cannot be cancelled.", nameof(Status));
        Status = StockCountStatus.Cancelled;
        Raise(actor);
    }

    /// <summary>EXACTLY the reported variances as <c>CountGain</c>/<c>CountLoss</c> drafts referencing the count (U-F7); none until committed.</summary>
    public IReadOnlyList<StockMovementDraft> BuildDrafts() =>
        Status != StockCountStatus.Committed
            ? []
            : _lines.Where(l => l.Variance is { } v && v != 0m).Select(l => new StockMovementDraft(
                BranchId, LocationId, l.SkuId, l.BatchId, l.Variance!.Value > 0m ? MovementKind.CountGain : MovementKind.CountLoss, l.Variance.Value,
                StockReferenceKind.StockCount, Id, l.Id, Number, null)).ToList();

    private void Raise(InventoryActor actor)
    {
        AddDomainEvent(new StockCountChangedEvent { CountId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void Require(StockCountStatus required, StockCountStatus target)
    {
        if (Status != required)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCountTransition, $"A {Status.DisplayName} count cannot move to {target.DisplayName}.", nameof(Status));
    }
}
