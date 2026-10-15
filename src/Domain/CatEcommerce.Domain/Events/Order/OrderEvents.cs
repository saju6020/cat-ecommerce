using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Order;

// HO-ORD-001 §11.5. Ids, instants, enumeration values, booleans and Money only -- never a name, an address, a phone number, a reference, a note or a
// reason as text (HO-ACC-001 U-X1, pinned by OrderingConventionTests.U_A8). Enumeration values travel as their int Value.
// Contracts for later areas: OrderPlacedEvent, OrderConfirmedEvent, OrderCancelledEvent, OrderDeliveredEvent (HO-SUB-001, HO-RPT-001, HO-POR-001).

public sealed class OrderPlacedEvent : Event
{
    public required Guid OrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required int Channel { get; init; }
    public required int PaymentMethod { get; init; }
    public required int FulfilmentMethod { get; init; }
    public required int Status { get; init; }
    public required Money GrandTotal { get; init; }
}

public sealed class OrderConfirmedEvent : Event
{
    public required Guid OrderId { get; init; }
    public Guid? InvoiceId { get; init; }
}

public sealed class OrderTransferReferenceSubmittedEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderPaymentHeldForApprovalEvent : Event
{
    public required Guid OrderId { get; init; }
    public required Money Amount { get; init; }
}

public sealed class OrderHeldPaymentRejectedEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderCancelledEvent : Event
{
    public required Guid OrderId { get; init; }
    public required int Reason { get; init; }
    public required bool WasPaid { get; init; }
    public required bool ByCustomer { get; init; }
}

public sealed class OrderPackingStartedEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderDispatchedEvent : Event
{
    public required Guid OrderId { get; init; }
    public required Guid CourierId { get; init; }
    public required bool HasTracking { get; init; }
}

public sealed class OrderReadyForCollectionEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderDeliveredEvent : Event
{
    public required Guid OrderId { get; init; }
    public required bool HadCodCollection { get; init; }
}

public sealed class OrderDeliveryFailedEvent : Event
{
    public required Guid OrderId { get; init; }
    public required int Reason { get; init; }
}

public sealed class OrderReturnedEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderRefundedEvent : Event
{
    public required Guid OrderId { get; init; }
}

public sealed class OrderLineReallocatedEvent : Event
{
    public required Guid OrderId { get; init; }
    public required Guid LineId { get; init; }
    public required Guid FromBatchId { get; init; }
    public required Guid ToBatchId { get; init; }
}

public sealed class CounterSaleCompletedEvent : Event
{
    public required Guid OrderId { get; init; }
    public required Money GrandTotal { get; init; }
}

/// <summary>HO-ORD-001 §8.16 -- money staff entered could not be recorded and was written aside for the accountants. Ids and the kind only: never the
/// reference, the amount's source or the error text.</summary>
public sealed class ReconciliationIssueRaisedEvent : Event
{
    public required Guid IssueId { get; init; }
    public Guid? OrderId { get; init; }
    public required int Kind { get; init; }
}

public sealed class ReturnRequestedEvent : Event
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid OrderId { get; init; }
    public required int Kind { get; init; }
}

public sealed class ReturnApprovedEvent : Event
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid OrderId { get; init; }
}

public sealed class ReturnRejectedEvent : Event
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid OrderId { get; init; }
}

public sealed class ReturnReceivedEvent : Event
{
    public required Guid ReturnRequestId { get; init; }
    public required Guid OrderId { get; init; }
}

/// <summary>HO-ORD-001 §11.5 -- a refund was recorded against an order (ADR-211). Ids, the method and the amount only: never the reference or the reason.</summary>
public sealed class RefundIssuedEvent : Event
{
    public required Guid RefundId { get; init; }
    public required Guid OrderId { get; init; }
    public required int Method { get; init; }
    public required Money Amount { get; init; }
}
