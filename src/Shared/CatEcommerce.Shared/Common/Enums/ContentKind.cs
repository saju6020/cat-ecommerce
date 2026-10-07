namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class ContentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ContentKind Page = new(1, nameof(Page));
    public static readonly ContentKind Faq = new(2, nameof(Faq));
    public static readonly ContentKind Banner = new(3, nameof(Banner));
}
