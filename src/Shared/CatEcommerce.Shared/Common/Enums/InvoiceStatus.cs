namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.6 / §10.1 (SRS Appendix C plus Discarded, ADR-180). After issue the status is derived from the totals (<c>InvoiceStatusRules.Derive</c>).</summary>
public sealed class InvoiceStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly InvoiceStatus Draft = new(1, nameof(Draft));
    public static readonly InvoiceStatus Issued = new(2, nameof(Issued));
    public static readonly InvoiceStatus PartPaid = new(3, nameof(PartPaid));
    public static readonly InvoiceStatus Paid = new(4, nameof(Paid));
    public static readonly InvoiceStatus Credited = new(5, nameof(Credited));
    public static readonly InvoiceStatus Discarded = new(6, nameof(Discarded));
}
