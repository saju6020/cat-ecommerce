namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-ACC-001 §10.1. Computed only, by <c>SessionStateRules.Classify</c> -- never persisted.
/// Both session aggregates and the read side call the same function so they cannot disagree.
/// </summary>
public sealed class SessionState(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SessionState Active = new(1, nameof(Active));
    public static readonly SessionState Expired = new(2, nameof(Expired));
    public static readonly SessionState Revoked = new(3, nameof(Revoked));
    public static readonly SessionState Ended = new(4, nameof(Ended));
    public static readonly SessionState IdleExpired = new(5, nameof(IdleExpired));
}
