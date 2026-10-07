namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-BRD-001 §12.8 P1, consumed by HO-BIL-001's <c>RecordPaymentCommand</c>: "accepts
/// <c>Reference { Kind = BoardingStayDeposit | BoardingStay | DaycareVisit, Id }</c>". Distinct from
/// <see cref="ChargeSourceKind"/> -- a payment reference, not a charge source.
/// </summary>
public sealed class BoardingPaymentReferenceKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingPaymentReferenceKind BoardingStayDeposit = new(1, nameof(BoardingStayDeposit));
    public static readonly BoardingPaymentReferenceKind BoardingStay = new(2, nameof(BoardingStay));
    public static readonly BoardingPaymentReferenceKind DaycareVisit = new(3, nameof(DaycareVisit));
}
