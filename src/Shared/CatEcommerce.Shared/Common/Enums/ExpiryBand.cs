namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1. Bands of the near-expiry report (R6).</summary>
public sealed class ExpiryBand(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ExpiryBand Expired = new(1, nameof(Expired));
    public static readonly ExpiryBand Within30Days = new(2, nameof(Within30Days));
    public static readonly ExpiryBand Within60Days = new(3, nameof(Within60Days));
    public static readonly ExpiryBand Within90Days = new(4, nameof(Within90Days));
}
