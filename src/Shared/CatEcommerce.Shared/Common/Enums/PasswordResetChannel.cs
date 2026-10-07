namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1. Which identifier a reset token was dispatched to.</summary>
public sealed class PasswordResetChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PasswordResetChannel Mobile = new(1, nameof(Mobile));
    public static readonly PasswordResetChannel Email = new(2, nameof(Email));
}
