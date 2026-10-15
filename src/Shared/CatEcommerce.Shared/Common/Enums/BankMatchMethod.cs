namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.1 / §10.1.</summary>
public sealed class BankMatchMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BankMatchMethod Auto = new(1, nameof(Auto));
    public static readonly BankMatchMethod Manual = new(2, nameof(Manual));
}
