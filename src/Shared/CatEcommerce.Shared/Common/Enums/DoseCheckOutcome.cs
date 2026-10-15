namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The result of comparing a dose with the formulary mg/kg range. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DoseCheckOutcome(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DoseCheckOutcome WithinRange = new(1, nameof(WithinRange));
    public static readonly DoseCheckOutcome BelowRange = new(2, nameof(BelowRange));
    public static readonly DoseCheckOutcome AboveRange = new(3, nameof(AboveRange));
    public static readonly DoseCheckOutcome NotCheckable = new(4, nameof(NotCheckable));
}
