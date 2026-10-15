namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- How serious a drug interaction is, ordered by <c>Value</c> (ADR-139: <c>Major</c> and <c>Contraindicated</c>
/// must be acknowledged). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class InteractionSeverity(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly InteractionSeverity Minor = new(1, nameof(Minor));
    public static readonly InteractionSeverity Moderate = new(2, nameof(Moderate));
    public static readonly InteractionSeverity Major = new(3, nameof(Major));
    public static readonly InteractionSeverity Contraindicated = new(4, nameof(Contraindicated));

    /// <summary>ADR-139 -- only these two interrupt the prescriber.</summary>
    public bool RequiresAcknowledgement => Value >= Major.Value;
}
