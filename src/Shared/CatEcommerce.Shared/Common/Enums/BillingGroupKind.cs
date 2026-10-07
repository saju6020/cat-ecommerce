namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-BIL-001 §8.3. "Put this on the stay's invoice" -- groups charges from several
/// sources (e.g. a clinic visit during a boarding stay) onto one billing group's invoice.
/// </summary>
public sealed class BillingGroupKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BillingGroupKind BoardingStay = new(1, nameof(BoardingStay));
}
