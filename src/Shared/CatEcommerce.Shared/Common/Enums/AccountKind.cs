namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1. Distinguishes which account root a reset token or lookup targets.</summary>
public sealed class AccountKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AccountKind Customer = new(1, nameof(Customer));
    public static readonly AccountKind Staff = new(2, nameof(Staff));
}
