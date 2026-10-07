namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class BannerPlacement(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BannerPlacement Home = new(1, nameof(Home));
    public static readonly BannerPlacement Shop = new(2, nameof(Shop));
    public static readonly BannerPlacement Portal = new(3, nameof(Portal));
}
