namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.10 / §10.1. Closed is terminal (ADR-188).</summary>
public sealed class CashUpStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CashUpStatus Open = new(1, nameof(Open));
    public static readonly CashUpStatus Closed = new(2, nameof(Closed));
}
