namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1.</summary>
public sealed class SessionEndReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SessionEndReason SignedOut = new(1, nameof(SignedOut));
    public static readonly SessionEndReason IdleTimeout = new(2, nameof(IdleTimeout));
}
