using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C14-C17.

/// <summary>C14 -- <c>ReorderPolicies.Manage</c>. An upsert by (SKU, location).</summary>
public sealed class SetReorderPolicyCommand : Command
{
    public required Guid BranchId { get; init; }
    public required Guid SkuId { get; init; }
    public required Guid LocationId { get; init; }
    public required decimal ReorderPoint { get; init; }
    public decimal? ReorderQuantity { get; init; }
    public Guid? PreferredSupplierId { get; init; }
}

/// <summary>C15 -- dispatched by <c>InventoryWorker</c> only (never over HTTP). A dedup hit is a success with nothing written.</summary>
public sealed class RaiseStockAlertCommand : Command
{
    public required Guid AlertId { get; init; }
    public required string Kind { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? SkuId { get; init; }
    public Guid? BatchId { get; init; }
    public Guid? LocationId { get; init; }
    public int? ThresholdDays { get; init; }
    public decimal? QuantityAtRaise { get; init; }
    public string? DedupKey { get; init; }
}

/// <summary>Worker: resolves a <c>LowStock</c> alert whose condition cleared.</summary>
public sealed class ResolveStockAlertCommand : Command
{
    public required Guid AlertId { get; init; }
    public string? Note { get; init; }
}

/// <summary><c>StockAlerts.Acknowledge</c>.</summary>
public sealed class AcknowledgeStockAlertCommand : Command
{
    public required Guid AlertId { get; init; }
    public required Guid BranchId { get; init; }
    public string? Note { get; init; }
}

/// <summary>C16 -- <c>Recalls.Manage</c>. <see cref="ConfirmLotNumber"/> must equal the lot (a recall is not a mis-click).</summary>
public sealed class RecallBatchCommand : Command
{
    public required Guid BatchId { get; init; }
    public required string ConfirmLotNumber { get; init; }
    public required string Reason { get; init; }
    public required string CustomerAdvice { get; init; }
}

public sealed class NotifyRecallRecipientsCommand : Command
{
    public required Guid BatchId { get; init; }
}

public sealed class WithdrawBatchRecallCommand : Command
{
    public required Guid BatchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class CloseBatchRecallCommand : Command
{
    public required Guid BatchId { get; init; }
    public required string Note { get; init; }
}

/// <summary>C17 -- <c>StorageBreaches.Report</c>.</summary>
public sealed class ReportStorageBreachCommand : Command
{
    public required Guid BreachId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid LocationId { get; init; }
    public required DateTimeOffset ObservedOn { get; init; }
    public required string Condition { get; init; }
    public decimal? TemperatureC { get; init; }
    public int? DurationMinutes { get; init; }
}

/// <summary><c>StorageBreaches.Manage</c>.</summary>
public sealed class QuarantineStorageBreachStockCommand : Command
{
    public required Guid BreachId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class CloseStorageBreachCommand : Command
{
    public required Guid BreachId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}

/// <summary>The recall trace holds customers' names and contact details, so reading it as a file is itself a sensitive act: ONE <c>BusinessEvent</c> audit row,
/// <c>RecallTraceExported</c>, written by the controller's dispatch after it has built the file (HO-ADM-001 ADR-051's rule applied to this export).</summary>
public sealed class RecordRecallTraceExportCommand : Command
{
    public required Guid BatchId { get; init; }
    public required int RowCount { get; init; }
    public string? IpAddress { get; init; }
}
