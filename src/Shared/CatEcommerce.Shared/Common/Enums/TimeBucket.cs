namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1.</summary>
public sealed class TimeBucket(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TimeBucket Day = new(1, nameof(Day));
    public static readonly TimeBucket Week = new(2, nameof(Week));
    public static readonly TimeBucket Month = new(3, nameof(Month));
}
