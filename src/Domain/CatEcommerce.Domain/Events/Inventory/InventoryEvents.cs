using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Inventory;

// HO-IMP-001 §11.5 -- ids, instants, enums, decimals and booleans only: no reason text, advice, notes or names
// (U-X1 reflects over this namespace and fails on any string property). Every aggregate mutation raises one, which
// is how the audit capture learns WHO did it (AuditChangeCaptureInterceptor reads the event's UserContext).
// Nothing subscribes to these inside Inventory (§11.6: no domain event handlers) -- BatchRecalledEvent and
// GoodsReceiptPostedEvent are published contracts for HO-ORD-001 and HO-SHP-001.

public sealed class StockLocationCreatedEvent : Event
{
    public required Guid LocationId { get; init; }
    public required Guid BranchId { get; init; }
    public required StockLocationKind Kind { get; init; }
}

public sealed class StockLocationChangedEvent : Event
{
    public required Guid LocationId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class ProductStorageProfileSetEvent : Event
{
    public required Guid ProductId { get; init; }
    public required StorageCondition Condition { get; init; }
}

public sealed class SupplierCreatedEvent : Event
{
    public required Guid SupplierId { get; init; }
}

public sealed class SupplierUpdatedEvent : Event
{
    public required Guid SupplierId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class PurchaseOrderDraftedEvent : Event
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid SupplierId { get; init; }
}

public sealed class PurchaseOrderIssuedEvent : Event
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid SupplierId { get; init; }
}

public sealed class PurchaseOrderCancelledEvent : Event
{
    public required Guid PurchaseOrderId { get; init; }
}

public sealed class PurchaseOrderClosedEvent : Event
{
    public required Guid PurchaseOrderId { get; init; }
}

public sealed class ConsignmentPlannedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class ConsignmentUpdatedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
}

public sealed class ConsignmentStatusChangedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
    public required ConsignmentStatus From { get; init; }
    public required ConsignmentStatus To { get; init; }
}

public sealed class ConsignmentDocumentAddedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
    public required Guid DocumentId { get; init; }
    public required ImportDocumentKind Kind { get; init; }
}

public sealed class ConsignmentCostsReplacedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
    public required bool AllocationNowStale { get; init; }
}

public sealed class LandedCostAllocatedEvent : Event
{
    public required Guid ConsignmentId { get; init; }
    public required int BatchCount { get; init; }
}

/// <summary>HO-SHP-001 may use it for back-in-stock (§11.5).</summary>
public sealed class GoodsReceiptPostedEvent : Event
{
    public required Guid GoodsReceiptId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<Guid> BatchIds { get; init; }
    public required IReadOnlyList<Guid> SkuIds { get; init; }
}

public sealed class BatchCreatedEvent : Event
{
    public required Guid BatchId { get; init; }
    public required Guid SkuId { get; init; }
}

public sealed class BatchQuarantinedEvent : Event
{
    public required Guid BatchId { get; init; }
}

public sealed class BatchReleasedEvent : Event
{
    public required Guid BatchId { get; init; }
}

/// <summary>The contract for HO-ORD-001 (§11.5): flag unshipped orders containing the batch -- idempotently, without enqueueing.</summary>
public sealed class BatchRecalledEvent : Event
{
    public required Guid BatchId { get; init; }
    public required Guid SkuId { get; init; }
    public required DateTimeOffset RecalledOn { get; init; }
}

public sealed class BatchRecallWithdrawnEvent : Event
{
    public required Guid BatchId { get; init; }
}

public sealed class BatchRecallNotifiedEvent : Event
{
    public required Guid BatchId { get; init; }
    public required int RecipientCount { get; init; }
}

public sealed class BatchRecallClosedEvent : Event
{
    public required Guid BatchId { get; init; }
}

public sealed class BatchWrittenOffEvent : Event
{
    public required Guid BatchId { get; init; }
}

public sealed class StockTransferPostedEvent : Event
{
    public required Guid TransferId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
    public required int LineCount { get; init; }
}

public sealed class StockAdjustmentRequestedEvent : Event
{
    public required Guid AdjustmentId { get; init; }
    public required AdjustmentStatus Status { get; init; }
    public required decimal ValueBase { get; init; }
}

public sealed class StockAdjustmentDecidedEvent : Event
{
    public required Guid AdjustmentId { get; init; }
    public required AdjustmentStatus Status { get; init; }
}

public sealed class StockCountOpenedEvent : Event
{
    public required Guid CountId { get; init; }
    public required Guid LocationId { get; init; }
}

public sealed class StockCountSubmittedEvent : Event
{
    public required Guid CountId { get; init; }
    public required decimal VarianceValueBase { get; init; }
}

public sealed class StockCountCommittedEvent : Event
{
    public required Guid CountId { get; init; }
    public required StockCountStatus Status { get; init; }
}

public sealed class StockCountChangedEvent : Event
{
    public required Guid CountId { get; init; }
    public required StockCountStatus Status { get; init; }
}

public sealed class ReorderPolicySetEvent : Event
{
    public required Guid SkuId { get; init; }
    public required Guid LocationId { get; init; }
}

public sealed class StockAlertRaisedEvent : Event
{
    public required Guid AlertId { get; init; }
    public required StockAlertKind Kind { get; init; }
}

public sealed class StockAlertChangedEvent : Event
{
    public required Guid AlertId { get; init; }
    public required InventoryAlertStatus Status { get; init; }
}

public sealed class StorageBreachReportedEvent : Event
{
    public required Guid BreachId { get; init; }
    public required Guid LocationId { get; init; }
    public required int AffectedBatchCount { get; init; }
}

public sealed class StorageBreachChangedEvent : Event
{
    public required Guid BreachId { get; init; }
    public required StorageBreachStatus Status { get; init; }
}
