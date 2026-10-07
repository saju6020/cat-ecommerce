namespace CatEcommerce.Shared.Common.Enums;

public sealed class NeuterStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NeuterStatus Intact = new(1, nameof(Intact));
    public static readonly NeuterStatus Neutered = new(2, nameof(Neutered));
    public static readonly NeuterStatus Unknown = new(3, nameof(Unknown));
}
