namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class RecoveryStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RecoveryStatus Recovering = new(1, nameof(Recovering));
    public static readonly RecoveryStatus Stable = new(2, nameof(Stable));
    public static readonly RecoveryStatus ReadyForDischarge = new(3, nameof(ReadyForDischarge));
}
