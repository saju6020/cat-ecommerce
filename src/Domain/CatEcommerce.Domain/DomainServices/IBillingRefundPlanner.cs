using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 §8.13 (Amended 2026-10-09, HO-ORD-001 architecture-gate fix, owner decision 4) -- <b>Billing's published planning port for a refund</b>. A producer
/// that pays money back (HO-ORD-001's refund, HO-BRD-001's early check-out) asks Billing <i>how</i> an amount should be credited and drawn, then dispatches
/// Billing's published <c>IssueCreditNoteCommand</c> and <c>IssueRefundCommand</c> with the answer. The producer no longer loads Billing's invoice or payment
/// aggregates or reads Billing's finder: what a line has already been credited, and what a payment still holds free, are Billing's facts and stay behind this
/// port. Read-only; implemented in Infrastructure/Billing.
/// </summary>
public interface IBillingRefundPlanner
{
    /// <summary>Spreads <paramref name="amount"/> over the invoice's remaining credit, line by line in invoice order; a credit of everything that is left is a
    /// <see cref="CreditPlan.Full"/> credit. Refuses with <c>InvoiceNotFound</c> or <c>CreditExceedsInvoice</c> otherwise.</summary>
    Task<CreditPlan> PlanCreditAsync(Guid invoiceId, decimal amount, CancellationToken ct = default);

    /// <summary>Draws <paramref name="amount"/> from the given payments in the order given (the producer passes its payments newest first), each capped at what the
    /// payment holds unallocated <b>now</b> -- so call it after the credit note has released an allocated excess.</summary>
    Task<RefundDrawPlan> PlanRefundAsync(IReadOnlyList<Guid> paymentIdsInOrder, decimal amount, CancellationToken ct = default);
}
