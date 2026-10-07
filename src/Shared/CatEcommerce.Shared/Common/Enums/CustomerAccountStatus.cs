namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1.</summary>
public sealed class CustomerAccountStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CustomerAccountStatus Active = new(1, nameof(Active));
    public static readonly CustomerAccountStatus Merged = new(2, nameof(Merged));
    public static readonly CustomerAccountStatus Anonymised = new(3, nameof(Anonymised));
}
