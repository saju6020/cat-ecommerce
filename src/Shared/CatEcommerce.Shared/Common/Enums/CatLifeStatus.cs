namespace CatEcommerce.Shared.Common.Enums;

public sealed class CatLifeStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CatLifeStatus Alive = new(1, nameof(Alive));
    public static readonly CatLifeStatus Deceased = new(2, nameof(Deceased));
}
