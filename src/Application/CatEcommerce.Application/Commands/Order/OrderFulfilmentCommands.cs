using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

// HO-ORD-001 §11.1 C16-C21 (Phase F). Every staff command carries the staff member's ACTIVE branch (HO-ADM-001 §8.3): an order of another branch does not
// exist for them (OrderNotFound, 404). Data only; ids minted in the controller; the caller comes from UserContext.

/// <summary>C16 (US-ORD-09) -- start packing a <c>Confirmed</c> order.</summary>
public sealed class StartPackingCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C16 (US-ORD-11) -- dispatch a packed delivery order with a courier and, optionally, a tracking number (it may be added later, C17). Checks the batches
/// (BR-14) and turns the reservation into the sale (<c>ConsumeReservationAsync</c>).</summary>
public sealed class DispatchOrderCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CourierId { get; init; }
    public string? TrackingNumber { get; init; }
}

/// <summary>C16 -- a packed pickup order is waiting at the branch (<c>Dispatched</c> = ready for collection). Checks the batches (BR-14).</summary>
public sealed class MarkReadyForCollectionCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C17 (US-ORD-11 AC2) -- the tracking number of a dispatched order, entered by hand. The customer is told only when it is the first.</summary>
public sealed class RecordTrackingNumberCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required string TrackingNumber { get; init; }
}

/// <summary>US-ORD-20 AC2 -- cash collected on delivery: how much, by a courier (named) or a staff member, and when.</summary>
public sealed record CodCollectionInput(decimal Amount, CollectorKind CollectorKind, string? CollectorName, Guid? CollectorStaffId, DateTimeOffset CollectedOn);

/// <summary>Money taken when a pay-on-collection pickup is handed over. An in-store card carries the terminal slip reference -- never a card number (C-5).</summary>
public sealed record CollectionPaymentInput(TenderMethod Method, decimal Amount, string? Reference);

/// <summary>C18 (US-ORD-20 AC2) -- hand-over: delivery of a delivery order, or collection of a pickup order. Cash on delivery needs the collection, pay on
/// collection needs the payment; a pickup order's reservation becomes the sale here.</summary>
public sealed class RecordDeliveryCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required DateTimeOffset DeliveredOn { get; init; }
    public CodCollectionInput? CodCollection { get; init; }
    public CollectionPaymentInput? CollectionPayment { get; init; }
}

/// <summary>C19 (US-ORD-20 AC3) -- a parcel that did not reach the customer (refused, undeliverable) or a pickup that was not collected.</summary>
public sealed class RecordDeliveryFailureCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }

    /// <summary>The id of the <c>DeliveryFailure</c> return this creates for a refused or undeliverable parcel (none for an uncollected pickup: nothing left the shop).</summary>
    public Guid ReturnRequestId { get; init; } = Guid.NewGuid();
    public required DeliveryFailureReason Reason { get; init; }
    public string? Note { get; init; }
}

/// <summary>C20 (US-ORD-18 AC2) -- a courier's tracking event from the webhook, already verified by signature. Applied at most once per
/// <c>(Provider, EventId)</c>.</summary>
public sealed class ApplyCourierTrackingEventCommand : Command
{
    public required string Provider { get; init; }
    public required string EventId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
}

/// <summary>C21 (US-IMP-07 AC2, order half) -- staff move one line to a specific batch at picking. A reason is mandatory; Inventory executes and audits it.</summary>
public sealed class ReallocateOrderLineCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid LineId { get; init; }
    public required Guid TargetBatchId { get; init; }
    public required string Reason { get; init; }
}
