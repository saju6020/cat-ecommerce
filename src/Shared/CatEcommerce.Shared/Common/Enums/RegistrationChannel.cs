namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1.</summary>
public sealed class RegistrationChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RegistrationChannel SelfService = new(1, nameof(SelfService));
    public static readonly RegistrationChannel Reception = new(2, nameof(Reception));
}
