namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ProblemCertainty(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProblemCertainty Provisional = new(1, nameof(Provisional));
    public static readonly ProblemCertainty Confirmed = new(2, nameof(Confirmed));
}
