using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BIL-001 §12.6 -- the table both the aggregate (its guards) and the read side
/// (<c>allowedActions</c>) use, so a button the UI shows is never refused by the aggregate.
/// </summary>
public static class InvoiceActionRules
{
    /// <summary>US-BIL-05 AC1: only a draft's content may change.</summary>
    public static bool CanChangeContent(InvoiceStatus status) => status == InvoiceStatus.Draft;

    /// <summary>A payment may be applied to an invoice that still has something outstanding.</summary>
    public static bool CanApplyPayment(InvoiceStatus status) =>
        status == InvoiceStatus.Issued || status == InvoiceStatus.PartPaid;

    /// <summary>Releasing an allocation (credit-note excess, payment reversal) touches any issued state.</summary>
    public static bool CanReleasePayment(InvoiceStatus status) => InvoiceStatusRules.IsIssuedState(status);

    public static bool CanApplyCredit(InvoiceStatus status) =>
        status == InvoiceStatus.Issued || status == InvoiceStatus.PartPaid || status == InvoiceStatus.Paid;

    public static IReadOnlyList<string> AllowedActions(InvoiceStatus status, decimal outstanding)
    {
        var actions = new List<string>();
        if (status == InvoiceStatus.Draft) actions.AddRange(["edit", "discard", "issue", "applyLoyalty"]);
        if (status != InvoiceStatus.Draft && status != InvoiceStatus.Discarded) actions.Add("pdf");
        if (CanApplyPayment(status) && outstanding > 0m) actions.AddRange(["recordPayment", "allocate", "applyCredit"]);
        if (CanApplyCredit(status)) actions.Add("creditNote");
        return actions;
    }
}

/// <summary>
/// HO-BIL-001 §12.4 -- the table the payment detail's <c>allowedActions</c> are read from, so a button the screen shows is never
/// refused by the aggregate (the <see cref="InvoiceActionRules"/> precedent).
/// </summary>
public static class PaymentActionRules
{
    public static IReadOnlyList<string> AllowedActions(PaymentStatus status, decimal unallocated, decimal refundedTotal, bool hasUnreleasedEarmark)
    {
        var actions = new List<string>();
        if (status == PaymentStatus.PendingApproval) actions.AddRange(["approve", "reject"]);

        if (status == PaymentStatus.Accepted)
        {
            actions.Add("receipt");
            if (unallocated > 0m) actions.AddRange(["allocate", "refund"]);
            if (hasUnreleasedEarmark) actions.Add("releaseEarmark");
            if (refundedTotal == 0m) actions.Add("reverse");
        }

        if (status == PaymentStatus.Reversed) actions.Add("receipt");
        return actions;
    }
}
