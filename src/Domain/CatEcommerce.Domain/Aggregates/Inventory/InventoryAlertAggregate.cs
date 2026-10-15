using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.13 (US-IMP-10, US-IMP-12, ADR-110) -- the <c>inventory.stock_alert</c> row. Named <c>InventoryAlertAggregate</c>, not
/// <c>StockAlertAggregate</c> as §8.1 says, because that name is already Shop's back-in-stock alert (HO-SHP-001); see HO-IMP-001 §1
/// Amended. <c>Open -> Acknowledged -> Resolved</c>; <c>Open -> Resolved</c>. Raised by the worker's timed passes (with dedup keys) or
/// by <c>IStockLedger.RecordShortfall</c>. No staff message is sent in v1 (OQ-11).
/// </summary>
public sealed class InventoryAlertAggregate : AggregateRoot
{
    public StockAlertKind Kind { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid? SkuId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid? LocationId { get; private set; }
    public int? ThresholdDays { get; private set; }
    public decimal? QuantityAtRaise { get; private set; }
    public StockReferenceKind? ReferenceKind { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? DedupKey { get; private set; }
    public DateTimeOffset RaisedOn { get; private set; }
    public InventoryAlertStatus Status { get; private set; } = InventoryAlertStatus.Open;
    public Guid? AcknowledgedBy { get; private set; }
    public DateTimeOffset? AcknowledgedOn { get; private set; }
    public DateTimeOffset? ResolvedOn { get; private set; }
    public string? ResolutionNote { get; private set; }

    private InventoryAlertAggregate() { }

    public InventoryAlertAggregate(Guid id) : base(id) { }

    public void Raise(StockAlertRaiseDto dto)
    {
        if (dto.DedupKey is { Length: > 200 })
            throw InventoryViolation.Of(BusinessViolationCodes.StockAlertNotOpen, "A dedup key cannot exceed 200 characters.", nameof(DedupKey));

        Kind = dto.Kind;
        BranchId = dto.BranchId;
        SkuId = dto.SkuId;
        BatchId = dto.BatchId;
        LocationId = dto.LocationId;
        ThresholdDays = dto.ThresholdDays;
        QuantityAtRaise = dto.QuantityAtRaise;
        ReferenceKind = dto.ReferenceKind;
        ReferenceId = dto.ReferenceId;
        DedupKey = dto.DedupKey;
        RaisedOn = dto.RaisedOn;
        Status = InventoryAlertStatus.Open;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StockAlertRaisedEvent { AlertId = Id, Kind = Kind, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void Acknowledge(Guid by, DateTimeOffset on, string? note, InventoryActor actor)
    {
        if (Status != InventoryAlertStatus.Open)
            throw InventoryViolation.Of(BusinessViolationCodes.StockAlertNotOpen, "Only an open alert can be acknowledged.", nameof(Status));
        if (note is { Length: > InventoryRules.NoteMaxLength })
            throw InventoryViolation.Of(BusinessViolationCodes.StockAlertNotOpen, "A note cannot exceed 500 characters.", nameof(note));

        Status = InventoryAlertStatus.Acknowledged;
        AcknowledgedBy = by;
        AcknowledgedOn = on;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Changed(actor);
    }

    public void Resolve(DateTimeOffset on, string? note, InventoryActor actor)
    {
        if (Status == InventoryAlertStatus.Resolved)
            throw InventoryViolation.Of(BusinessViolationCodes.StockAlertNotOpen, "This alert is already resolved.", nameof(Status));

        Status = InventoryAlertStatus.Resolved;
        ResolvedOn = on;
        if (!string.IsNullOrWhiteSpace(note)) ResolutionNote = note.Trim();
        Changed(actor);
    }

    private void Changed(InventoryActor actor)
    {
        AddDomainEvent(new StockAlertChangedEvent { AlertId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }
}
