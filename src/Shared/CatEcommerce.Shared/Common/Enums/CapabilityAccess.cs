namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-ACC-001 §10.1. Least to most permissive, in that order, so a union across roles is a
/// numeric max.
/// </summary>
public sealed class CapabilityAccess(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CapabilityAccess None = new(1, nameof(None));
    public static readonly CapabilityAccess ReadOnly = new(2, nameof(ReadOnly));
    public static readonly CapabilityAccess CreateEdit = new(3, nameof(CreateEdit));
    public static readonly CapabilityAccess Full = new(4, nameof(Full));
}
