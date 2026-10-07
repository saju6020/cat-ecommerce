namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.3/§10.1.</summary>
public sealed class SmsEncoding(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SmsEncoding Gsm7 = new(1, nameof(Gsm7));
    public static readonly SmsEncoding Ucs2 = new(2, nameof(Ucs2));
}
