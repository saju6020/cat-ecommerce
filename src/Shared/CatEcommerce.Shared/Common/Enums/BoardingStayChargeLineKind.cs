namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-BRD-001 §8.15 (<c>StayChargeLine.Kind</c>). Reused as the <c>Kind</c> field of the
/// lines <c>IBoardingBillableSource.GetUninvoicedLinesAsync</c> returns to HO-BIL-001 (§12.8 P2).
/// </summary>
public sealed class BoardingStayChargeLineKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingStayChargeLineKind RateUnit = new(1, nameof(RateUnit));
    public static readonly BoardingStayChargeLineKind AdditionalCat = new(2, nameof(AdditionalCat));
    public static readonly BoardingStayChargeLineKind LongStayDiscount = new(3, nameof(LongStayDiscount));
    public static readonly BoardingStayChargeLineKind Adjustment = new(4, nameof(Adjustment));
    public static readonly BoardingStayChargeLineKind DepositRefundDue = new(5, nameof(DepositRefundDue));
    public static readonly BoardingStayChargeLineKind EarlyCheckoutCredit = new(6, nameof(EarlyCheckoutCredit));
}
