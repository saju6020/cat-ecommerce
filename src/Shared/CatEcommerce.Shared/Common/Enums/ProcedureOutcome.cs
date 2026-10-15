namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ProcedureOutcome(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProcedureOutcome Completed = new(1, nameof(Completed));
    public static readonly ProcedureOutcome Abandoned = new(2, nameof(Abandoned));
}
