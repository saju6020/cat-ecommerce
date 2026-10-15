namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.9 / §10.1. Money out. No online member until the gateway exists (ADR-182).</summary>
public sealed class RefundMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RefundMethod CashInStore = new(1, nameof(CashInStore));
    public static readonly RefundMethod BankTransfer = new(2, nameof(BankTransfer));
}
