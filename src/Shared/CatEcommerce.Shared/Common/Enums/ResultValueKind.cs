namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ResultValueKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ResultValueKind Numeric = new(1, nameof(Numeric));
    public static readonly ResultValueKind Qualitative = new(2, nameof(Qualitative));
}
