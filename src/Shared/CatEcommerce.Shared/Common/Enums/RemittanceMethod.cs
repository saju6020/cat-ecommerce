namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.1 / §10.1. How a courier remitted collected cash-on-delivery money.</summary>
public sealed class RemittanceMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RemittanceMethod Cash = new(1, nameof(Cash));
    public static readonly RemittanceMethod BankTransfer = new(2, nameof(BankTransfer));
}
