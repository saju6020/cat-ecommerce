namespace CatEcommerce.Shared.Common.Enums;

public sealed class AllergySeverity(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AllergySeverity Mild = new(1, nameof(Mild));
    public static readonly AllergySeverity Moderate = new(2, nameof(Moderate));
    public static readonly AllergySeverity Severe = new(3, nameof(Severe));
}
