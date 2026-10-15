namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.5. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class CostComponentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CostComponentKind Freight = new(1, nameof(Freight));
    public static readonly CostComponentKind Duty = new(2, nameof(Duty));
    public static readonly CostComponentKind Clearing = new(3, nameof(Clearing));
    public static readonly CostComponentKind Insurance = new(4, nameof(Insurance));
    public static readonly CostComponentKind Other = new(5, nameof(Other));
}
