namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The state of an owner repeat request. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class RepeatRequestStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RepeatRequestStatus Requested = new(1, nameof(Requested));
    public static readonly RepeatRequestStatus Dispensed = new(2, nameof(Dispensed));
    public static readonly RepeatRequestStatus Declined = new(3, nameof(Declined));
    public static readonly RepeatRequestStatus Withdrawn = new(4, nameof(Withdrawn));
}
