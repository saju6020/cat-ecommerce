namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §10.1. A bank statement line is money in (Credit) or out (Debit).</summary>
public sealed class BankEntryDirection(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BankEntryDirection Credit = new(1, nameof(Credit));
    public static readonly BankEntryDirection Debit = new(2, nameof(Debit));
}
