namespace CatEcommerce.Api.Contracts.Order;

/// <summary>HO-ORD-001 §12.4 -- <c>POST /api/customers/me/orders/{orderId}/transfer-reference</c>.</summary>
public sealed class SubmitTransferReferenceRequest
{
    public string Reference { get; init; } = default!;
}

/// <summary><c>POST /api/staff/orders/{orderId}/payment/bank-transfer</c>: the amount staff verified (exactly the total), the bank reference and when the money
/// arrived. <c>ExpectedVersion</c> guards a stale screen.</summary>
public sealed class ConfirmBankTransferRequest
{
    public int ExpectedVersion { get; init; }
    public decimal Amount { get; init; }
    public string BankReference { get; init; } = default!;
    public DateTimeOffset ReceivedOn { get; init; }
}

public sealed class ApproveHeldPaymentRequest
{
    public int ExpectedVersion { get; init; }
}

public sealed class RejectHeldPaymentRequest
{
    public string Note { get; init; } = default!;
}

public sealed class CancelOrderRequest
{
    public string? Note { get; init; }
}

public sealed class ResolveReconciliationIssueRequest
{
    public string Note { get; init; } = default!;
}
