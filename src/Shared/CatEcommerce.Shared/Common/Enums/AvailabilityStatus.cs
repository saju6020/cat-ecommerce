namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.7/10.1 -- computed only, never stored on a command.</summary>
public sealed class AvailabilityStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AvailabilityStatus InStock = new(1, nameof(InStock));
    public static readonly AvailabilityStatus OutOfStock = new(2, nameof(OutOfStock));
}
