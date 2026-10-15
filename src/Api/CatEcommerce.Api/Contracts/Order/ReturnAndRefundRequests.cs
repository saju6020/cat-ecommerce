namespace CatEcommerce.Api.Contracts.Order;

public sealed class ReturnLineRequest
{
    public Guid OrderLineId { get; init; }
    public int Quantity { get; init; }
    public string Reason { get; init; } = default!;
    public string Condition { get; init; } = default!;
    public string? CustomerNote { get; init; }
}

/// <summary><c>POST …/orders/{orderId}/returns</c> -- a customer's own, or staff on the customer's behalf.</summary>
public sealed class RequestReturnRequest
{
    public IReadOnlyList<ReturnLineRequest> Lines { get; init; } = [];
}

public sealed class ApproveReturnLineRequest
{
    public Guid OrderLineId { get; init; }
    public bool DefectConfirmed { get; init; }
    public string? DefectNote { get; init; }
}

public sealed class ApproveReturnRequest
{
    public IReadOnlyList<ApproveReturnLineRequest> Lines { get; init; } = [];
    public string? Note { get; init; }
}

public sealed class RejectReturnRequest
{
    public string Note { get; init; } = default!;
}

public sealed class ReceiveReturnLineRequest
{
    public Guid OrderLineId { get; init; }
    public int QuantityReceived { get; init; }
}

public sealed class ReceiveReturnRequest
{
    public IReadOnlyList<ReceiveReturnLineRequest> Lines { get; init; } = [];
}

/// <summary><c>POST /api/staff/orders/{orderId}/refunds</c>. <c>Method</c> is <c>CashInStore</c> (with the receipt number), <c>BankTransfer</c> (with the transfer reference) or
/// <c>StoreCredit</c> (unavailable until Billing builds it). The reference is never a card number.</summary>
public sealed class IssueRefundRequest
{
    public Guid? ReturnRequestId { get; init; }
    public string Method { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
    public string Reason { get; init; } = default!;
}
