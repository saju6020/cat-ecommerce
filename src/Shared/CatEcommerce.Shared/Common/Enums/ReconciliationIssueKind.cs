namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. What a reconciliation issue is about (ADR-205).</summary>
public sealed class ReconciliationIssueKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReconciliationIssueKind TransferConfirmationFailed = new(1, nameof(TransferConfirmationFailed));
    public static readonly ReconciliationIssueKind PaymentApprovalFailed = new(2, nameof(PaymentApprovalFailed));
    public static readonly ReconciliationIssueKind DeliveryPaymentFailed = new(3, nameof(DeliveryPaymentFailed));
    public static readonly ReconciliationIssueKind CounterSaleFailed = new(4, nameof(CounterSaleFailed));
    public static readonly ReconciliationIssueKind RefundFailed = new(5, nameof(RefundFailed));
}
