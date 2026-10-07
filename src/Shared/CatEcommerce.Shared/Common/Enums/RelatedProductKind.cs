namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.10/10.1.</summary>
public sealed class RelatedProductKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RelatedProductKind FrequentlyBoughtTogether = new(1, nameof(FrequentlyBoughtTogether));
    public static readonly RelatedProductKind UpSell = new(2, nameof(UpSell));
}
