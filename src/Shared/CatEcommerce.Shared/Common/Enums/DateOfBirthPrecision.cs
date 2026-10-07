namespace CatEcommerce.Shared.Common.Enums;

public sealed class DateOfBirthPrecision(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DateOfBirthPrecision Exact = new(1, nameof(Exact));
    public static readonly DateOfBirthPrecision Estimated = new(2, nameof(Estimated));
}
