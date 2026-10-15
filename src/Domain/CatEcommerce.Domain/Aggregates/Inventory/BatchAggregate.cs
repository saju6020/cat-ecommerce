using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.6 / §8.6.1 (US-IMP-04/06/09/11). One per ACCEPTED receipt line -- the same lot arriving twice is two batches.
/// The status here is only the lifecycle (<c>Available</c>, <c>Quarantined</c>, <c>Recalled</c>, <c>WrittenOff</c>);
/// <b>"expired" is derived</b> from <c>ExpiresOn</c> in branch-local time (<see cref="BatchExpiryRules"/>), never stored (ADR-101).
/// Stock is not here -- it is the ledger's (<c>stock_position</c>). <see cref="Recall"/>ing is a single row update, which is what
/// makes every location unusable at once (US-IMP-11 AC1, ADR-104).
/// </summary>
public sealed class BatchAggregate : AggregateRoot
{
    private readonly List<BatchStatusChange> _statusHistory = [];

    public Guid SkuId { get; private set; }
    public string SkuCode { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string StockUnit { get; private set; } = default!;
    public StockClass StockClass { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public string LotNumber { get; private set; } = default!;
    public DateOnly? ManufacturedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid GoodsReceiptLineId { get; private set; }
    public Guid? ConsignmentId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string? OriginCountryCode { get; private set; }
    public DateTimeOffset ReceivedOn { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal PurchaseUnitCostAmount { get; private set; }
    public string PurchaseUnitCostCurrency { get; private set; } = default!;
    public decimal PurchaseUnitCostBase { get; private set; }
    public BatchStatus Status { get; private set; } = BatchStatus.Available;
    public string? StatusReason { get; private set; }
    public DateTimeOffset StatusChangedOn { get; private set; }
    public Guid StatusChangedBy { get; private set; }
    public BatchRecall? CurrentRecall { get; private set; }
    public IReadOnlyList<BatchStatusChange> StatusHistory => _statusHistory;

    private BatchAggregate() { }

    public BatchAggregate(Guid id) : base(id) { }

    /// <summary>Created only by a goods receipt (ADR-105): <c>Available</c>, or <c>Quarantined</c> with <c>HoldForInspection</c>.</summary>
    public void Create(NewBatchDto dto)
    {
        if (dto.InitialStatus != BatchStatus.Available && dto.InitialStatus != BatchStatus.Quarantined)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidBatchTransition, "A batch is created Available or Quarantined.", nameof(Status));

        SkuId = dto.SkuId;
        SkuCode = dto.Sku.SkuCode;
        DisplayName = dto.Sku.DisplayName;
        StockUnit = dto.Sku.StockUnit;
        StockClass = dto.Sku.StockClass;
        BranchId = dto.BranchId;
        LotNumber = dto.LotNumber;
        ManufacturedOn = dto.ManufacturedOn;
        ExpiresOn = dto.ExpiresOn;
        GoodsReceiptId = dto.GoodsReceiptId;
        GoodsReceiptLineId = dto.GoodsReceiptLineId;
        ConsignmentId = dto.ConsignmentId;
        SupplierId = dto.SupplierId;
        OriginCountryCode = dto.OriginCountryCode;
        ReceivedOn = dto.ReceivedOn;
        ReceivedQuantity = dto.ReceivedQuantity;
        PurchaseUnitCostAmount = dto.PurchaseUnitCostAmount;
        PurchaseUnitCostCurrency = dto.PurchaseUnitCostCurrency;
        PurchaseUnitCostBase = dto.PurchaseUnitCostBase;
        Status = dto.InitialStatus;
        StatusChangedOn = dto.ReceivedOn;
        StatusChangedBy = dto.Actor.UserContext.UserId;
        StatusReason = dto.InitialStatus == BatchStatus.Quarantined ? "Held for inspection on receipt." : null;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new BatchCreatedEvent { BatchId = Id, SkuId = SkuId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void Quarantine(string? reason, DateTimeOffset on, InventoryActor actor)
    {
        Move(BatchTransitionRules.Quarantine, RequireReason(reason), on, actor);
        AddDomainEvent(new BatchQuarantinedEvent { BatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    public void Release(DateTimeOffset on, InventoryActor actor)
    {
        Move(BatchTransitionRules.Release, null, on, actor);
        AddDomainEvent(new BatchReleasedEvent { BatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Declares a recall. The lot-number confirmation ("a recall is not a mis-click") is checked here too.</summary>
    public void Recall(BatchRecallDto dto, string confirmLotNumber)
    {
        if (!string.Equals(confirmLotNumber?.Trim(), LotNumber, StringComparison.Ordinal))
            throw InventoryViolation.Of(BusinessViolationCodes.RecallConfirmationMismatch, "Type the batch's lot number exactly to confirm the recall.", nameof(confirmLotNumber));
        var reason = InventoryViolation.Normalise(dto.Reason);
        var advice = InventoryViolation.Normalise(dto.CustomerAdvice);
        if (reason.Length == 0 || reason.Length > RecallRules.ReasonMaxLength)
            throw InventoryViolation.Of(BusinessViolationCodes.RecallConfirmationMismatch, "A recall reason (at most 1000 characters) is required.", nameof(dto.Reason));
        if (advice.Length == 0 || advice.Length > RecallRules.AdviceMaxLength)
            throw InventoryViolation.Of(BusinessViolationCodes.RecallConfirmationMismatch, "Customer advice (at most 500 characters) is required.", nameof(dto.CustomerAdvice));

        Move(BatchTransitionRules.Recall, reason, dto.DeclaredOn, dto.Actor);
        CurrentRecall = new BatchRecall(dto.RecallNumber, reason, advice, dto.DeclaredOn, dto.Actor.UserContext.UserId);

        AddDomainEvent(new BatchRecalledEvent { BatchId = Id, SkuId = SkuId, RecalledOn = dto.DeclaredOn, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>A recall declared by mistake: goes to <c>Quarantined</c>, never straight back to sale. Pending notices then fail
    /// <c>BatchRecallGuard</c> and are cancelled.</summary>
    public void WithdrawRecall(string? reason, DateTimeOffset on, InventoryActor actor)
    {
        Move(BatchTransitionRules.WithdrawRecall, RequireReason(reason), on, actor);
        AddDomainEvent(new BatchRecallWithdrawnEvent { BatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    public void WriteOff(string? reason, DateTimeOffset on, InventoryActor actor)
    {
        Move(BatchTransitionRules.WriteOff, RequireReason(reason), on, actor);
        AddDomainEvent(new BatchWrittenOffEvent { BatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    public void RecordRecallNotification(int count, DateTimeOffset on, Guid by, InventoryActor actor)
    {
        if (Status != BatchStatus.Recalled || CurrentRecall is null)
            throw InventoryViolation.Of(BusinessViolationCodes.RecallNotActive, "This batch is not under recall.", nameof(Status));

        CurrentRecall.RecordNotification(count, on, by);
        AddDomainEvent(new BatchRecallNotifiedEvent { BatchId = Id, RecipientCount = count, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Records completion once no stock is held (the handler checks that, <c>RecallStockStillHeld</c>); the batch stays <c>Recalled</c>.</summary>
    public void CloseRecall(string? note, DateTimeOffset on, InventoryActor actor)
    {
        if (Status != BatchStatus.Recalled || CurrentRecall is null)
            throw InventoryViolation.Of(BusinessViolationCodes.RecallNotActive, "This batch is not under recall.", nameof(Status));

        CurrentRecall.Close(on, RequireReason(note));
        AddDomainEvent(new BatchRecallClosedEvent { BatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void Move(string action, string? reason, DateTimeOffset on, InventoryActor actor)
    {
        var to = BatchTransitionRules.Next(Status, action)
            ?? throw InventoryViolation.Of(BusinessViolationCodes.InvalidBatchTransition, $"A {Status.DisplayName} batch cannot be {action.Replace('-', ' ')}ed.", nameof(Status));

        if (action == BatchTransitionRules.WithdrawRecall && CurrentRecall is null)
            throw InventoryViolation.Of(BusinessViolationCodes.RecallNotActive, "This batch is not under recall.", nameof(Status));

        _statusHistory.Add(new BatchStatusChange(Guid.NewGuid(), Status, to, reason, on, actor.UserContext.UserId, _statusHistory.Count + 1));
        Status = to;
        StatusReason = reason;
        StatusChangedOn = on;
        StatusChangedBy = actor.UserContext.UserId;
    }

    private static string RequireReason(string? reason)
    {
        var text = InventoryViolation.Normalise(reason);
        if (text.Length < InventoryRules.ReasonMinLength || text.Length > 1000)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidBatchTransition, "A reason of at least 5 characters (at most 1000) is required.", "Reason");
        return text;
    }
}
