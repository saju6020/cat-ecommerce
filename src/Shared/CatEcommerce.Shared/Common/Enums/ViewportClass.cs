namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- Narrow under 768 px, Medium under 1280 px, otherwise Wide.</summary>
public sealed class ViewportClass(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ViewportClass Narrow = new(1, nameof(Narrow));
    public static readonly ViewportClass Medium = new(2, nameof(Medium));
    public static readonly ViewportClass Wide = new(3, nameof(Wide));
}
