using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BIL-001 §8.6. After issue the status is <b>derived</b> from the totals on every posting by this
/// one function; <c>outstanding = Total - CreditedTotal - PaidTotal</c> (never negative).
/// </summary>
public static class InvoiceStatusRules
{
    public static decimal Outstanding(decimal total, decimal paid, decimal credited) => total - credited - paid;

    public static InvoiceStatus Derive(decimal total, decimal paid, decimal credited)
    {
        if (credited > 0m && credited >= total) return InvoiceStatus.Credited;

        var outstanding = Outstanding(total, paid, credited);
        if (outstanding == 0m && (paid > 0m || total == 0m)) return InvoiceStatus.Paid;
        if (paid > 0m) return InvoiceStatus.PartPaid;
        return InvoiceStatus.Issued;
    }

    /// <summary>Issued, part-paid, paid or credited: the states a posting (payment, release, credit) may touch.</summary>
    public static bool IsIssuedState(InvoiceStatus status) =>
        status == InvoiceStatus.Issued || status == InvoiceStatus.PartPaid || status == InvoiceStatus.Paid || status == InvoiceStatus.Credited;
}
