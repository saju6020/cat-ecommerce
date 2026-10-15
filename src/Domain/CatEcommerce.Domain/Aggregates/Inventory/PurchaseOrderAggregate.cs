using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.5 (US-IMP-02). <c>Draft -> Issued -> Closed</c>; <c>Draft|Issued -> Cancelled</c> (only while nothing has been
/// received). Receipt progress is DERIVED from posted receipts (ADR-112) -- receiving never edits this aggregate, so there is no
/// counter here to drift. Supplier-active and "has receipts" are facts the handler reads and hands in.
/// </summary>
public sealed class PurchaseOrderAggregate : AggregateRoot
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public string Number { get; private set; } = default!;
    public Guid SupplierId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid DeliverToLocationId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateOnly ExpectedDeliveryOn { get; private set; }
    public string? Notes { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public string? StatusReason { get; private set; }
    public DateTimeOffset? IssuedOn { get; private set; }
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    private PurchaseOrderAggregate() { }

    public PurchaseOrderAggregate(Guid id) : base(id) { }

    public void CreateDraft(PurchaseOrderDraftDto dto)
    {
        Number = dto.Number;
        Fill(dto);

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new PurchaseOrderDraftedEvent { PurchaseOrderId = Id, SupplierId = SupplierId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void ReplaceDraft(PurchaseOrderDraftDto dto)
    {
        RequireStatus(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Draft);
        Fill(dto);

        AddDomainEvent(new PurchaseOrderDraftedEvent { PurchaseOrderId = Id, SupplierId = SupplierId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <param name="branchToday">The branch-local date (ADR-045).</param>
    public void Issue(DateOnly branchToday, DateTimeOffset on, InventoryActor actor)
    {
        RequireStatus(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Issued);
        if (_lines.Count == 0)
            throw InventoryViolation.Of(BusinessViolationCodes.PurchaseOrderLinesRequired, "A purchase order needs at least one line before it can be issued.", nameof(Lines));
        if (ExpectedDeliveryOn < branchToday)
            throw InventoryViolation.Of(BusinessViolationCodes.ExpectedDeliveryInPast, "The expected delivery date is in the past.", nameof(ExpectedDeliveryOn));

        Status = PurchaseOrderStatus.Issued;
        IssuedOn = on;

        AddDomainEvent(new PurchaseOrderIssuedEvent { PurchaseOrderId = Id, SupplierId = SupplierId, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <param name="hasReceipts">Read by the handler: any posted receipt line references this order.</param>
    public void Cancel(string? reason, bool hasReceipts, InventoryActor actor)
    {
        if (Status != PurchaseOrderStatus.Draft && Status != PurchaseOrderStatus.Issued)
            throw InvalidTransition(PurchaseOrderStatus.Cancelled);
        if (hasReceipts)
            throw InventoryViolation.Of(BusinessViolationCodes.PurchaseOrderHasReceipts, "Goods have already been received against this order; short-close it instead.", nameof(Status));
        var text = RequireReason(reason);

        Status = PurchaseOrderStatus.Cancelled;
        StatusReason = text;

        AddDomainEvent(new PurchaseOrderCancelledEvent { PurchaseOrderId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Short-closes a partially received order -- the outstanding quantity is still shown afterwards.</summary>
    public void Close(string? reason, InventoryActor actor)
    {
        RequireStatus(PurchaseOrderStatus.Issued, PurchaseOrderStatus.Closed);
        var text = RequireReason(reason);

        Status = PurchaseOrderStatus.Closed;
        StatusReason = text;

        AddDomainEvent(new PurchaseOrderClosedEvent { PurchaseOrderId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void Fill(PurchaseOrderDraftDto dto)
    {
        if (dto.Lines.Count == 0 || dto.Lines.Count > InventoryRules.MaxPurchaseOrderLines)
            throw InventoryViolation.Of(BusinessViolationCodes.PurchaseOrderLinesRequired, $"A purchase order has between 1 and {InventoryRules.MaxPurchaseOrderLines} lines.", nameof(Lines));
        if (!InventoryRules.IsValidCurrencyCode(dto.CurrencyCode))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCurrencyCode, "The currency must be a three-letter ISO-4217 code in upper case.", nameof(CurrencyCode));
        if (dto.Notes is { Length: > 1000 })
            throw InventoryViolation.Of(BusinessViolationCodes.PurchaseOrderLinesRequired, "Notes cannot exceed 1000 characters.", nameof(Notes));

        var lines = new List<PurchaseOrderLine>();
        for (var i = 0; i < dto.Lines.Count; i++)
        {
            var line = dto.Lines[i];
            if (lines.Any(l => l.SkuId == line.SkuId))
                throw InventoryViolation.Of(BusinessViolationCodes.DuplicatePurchaseOrderSku, "A SKU can appear on a purchase order only once.", $"Lines[{i}].SkuId");

            var quantity = StockQuantity.Create(line.OrderedQuantity, line.AllowsFractionalQuantity, $"Lines[{i}].OrderedQuantity");
            var cost = new UnitCost(line.UnitCostAmount, dto.CurrencyCode);
            lines.Add(new PurchaseOrderLine(line.LineId, line.SkuId, line.Sku.SkuCode, line.Sku.DisplayName, line.Sku.StockUnit, quantity.Value, cost.Amount));
        }

        SupplierId = dto.SupplierId;
        BranchId = dto.BranchId;
        DeliverToLocationId = dto.DeliverToLocationId;
        CurrencyCode = dto.CurrencyCode;
        ExpectedDeliveryOn = dto.ExpectedDeliveryOn;
        Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        Status = PurchaseOrderStatus.Draft;

        _lines.Clear();
        _lines.AddRange(lines);
    }

    private void RequireStatus(PurchaseOrderStatus required, PurchaseOrderStatus target)
    {
        if (Status != required) throw InvalidTransition(target);
    }

    private Exceptions.DomainRuleViolationException InvalidTransition(PurchaseOrderStatus to) =>
        InventoryViolation.Of(BusinessViolationCodes.InvalidPurchaseOrderTransition, $"A purchase order cannot move from {Status.DisplayName} to {to.DisplayName}.", nameof(Status));

    private static string RequireReason(string? reason)
    {
        var text = InventoryViolation.Normalise(reason);
        if (text.Length == 0 || text.Length > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.CloseReasonRequired, "A reason (at most 500 characters) is required.", "Reason");
        return text;
    }
}
