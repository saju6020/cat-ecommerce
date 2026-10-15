namespace CatEcommerce.Api.Contracts.Order;

/// <summary><c>POST /api/staff/orders/{orderId}/dispatch</c>. The tracking number may follow later (<c>PUT …/tracking</c>).</summary>
public sealed class DispatchOrderRequest
{
    public Guid CourierId { get; init; }
    public string? TrackingNumber { get; init; }
}

public sealed class RecordTrackingNumberRequest
{
    public string TrackingNumber { get; init; } = default!;
}

/// <summary>Cash collected on delivery: <c>CollectorKind</c> is <c>Courier</c> (name the rider) or <c>Staff</c> (give the staff account).</summary>
public sealed class CodCollectionRequest
{
    public decimal Amount { get; init; }
    public string CollectorKind { get; init; } = default!;
    public string? CollectorName { get; init; }
    public Guid? CollectorStaffId { get; init; }
    public DateTimeOffset CollectedOn { get; init; }
}

/// <summary>Money taken when a pickup is collected: <c>Method</c> is <c>Cash</c> or <c>InStoreCard</c> (with the terminal slip reference -- never a card number).</summary>
public sealed class CollectionPaymentRequest
{
    public string Method { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
}

/// <summary><c>POST /api/staff/orders/{orderId}/delivery</c> -- delivery of a delivery order or collection of a pickup order.</summary>
public sealed class RecordDeliveryRequest
{
    public int ExpectedVersion { get; init; }
    public DateTimeOffset DeliveredOn { get; init; }
    public CodCollectionRequest? CodCollection { get; init; }
    public CollectionPaymentRequest? CollectionPayment { get; init; }
}

public sealed class RecordDeliveryFailureRequest
{
    public string Reason { get; init; } = default!;
    public string? Note { get; init; }
}

public sealed class ReallocateOrderLineRequest
{
    public Guid TargetBatchId { get; init; }
    public string Reason { get; init; } = default!;
}
