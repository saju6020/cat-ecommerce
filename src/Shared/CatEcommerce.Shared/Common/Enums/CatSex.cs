namespace CatEcommerce.Shared.Common.Enums;

public sealed class CatSex(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CatSex Male = new(1, nameof(Male));
    public static readonly CatSex Female = new(2, nameof(Female));
    public static readonly CatSex Unknown = new(3, nameof(Unknown));
}
