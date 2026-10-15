namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The state of a prescription-diet authorisation. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DietAuthorisationStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DietAuthorisationStatus Active = new(1, nameof(Active));
    public static readonly DietAuthorisationStatus Expired = new(2, nameof(Expired));
    public static readonly DietAuthorisationStatus Revoked = new(3, nameof(Revoked));
}
