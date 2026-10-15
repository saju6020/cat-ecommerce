namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-ORD-001 §10.1 (<c>RefundMethod</c> there; renamed because HO-BIL-001 already owns a <see cref="RefundMethod"/> with two members) -- how an order refund is
/// paid back (US-ORD-15 AC1). <c>StoreCredit</c> exists only when HO-BIL-001 builds store credit (US-BIL-08, Could); until then it is refused with
/// <c>RefundMethodUnavailable</c> (OQ-8). Persisted by <c>Value</c> (ADR-022).
/// </summary>
public sealed class OrderRefundMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly OrderRefundMethod CashInStore = new(1, nameof(CashInStore));
    public static readonly OrderRefundMethod BankTransfer = new(2, nameof(BankTransfer));
    public static readonly OrderRefundMethod StoreCredit = new(3, nameof(StoreCredit));
}
