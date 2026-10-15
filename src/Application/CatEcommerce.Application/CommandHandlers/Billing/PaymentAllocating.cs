using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>One allocation that was applied: the invoice, its number and the outstanding that remains on it.</summary>
public sealed record AppliedAllocation(Guid InvoiceId, string Number, decimal Amount, decimal OutstandingAfter);

/// <summary>
/// HO-BIL-001 §8.8 -- an earmarked payment (a deposit) allocates only to an invoice that contains a line of the order or stay it
/// is set aside for, unless the earmark was released (<c>EarmarkMismatch</c>, U-D9). Pure: it reads the invoice's own lines.
/// </summary>
public static class EarmarkMatching
{
    public static bool Matches(PaymentEarmark? earmark, InvoiceAggregate invoice)
    {
        if (earmark is null || earmark.IsReleased) return true;

        return invoice.Lines.Any(line =>
            (line.SourceKind == ChargeSourceKind.Order && earmark.Kind == EarmarkKind.Order && line.SourceId == earmark.SourceId) ||
            (earmark.Kind == EarmarkKind.BoardingStay &&
             ((line.SourceKind == ChargeSourceKind.BoardingStay && line.SourceId == earmark.SourceId) ||
              (line.BillingGroupKind == BillingGroupKind.BoardingStay && line.BillingGroupId == earmark.SourceId))));
    }
}

/// <summary>
/// The posting both a recording, an approval and an allocation share (ADR-184): for each requested invoice, check it is the
/// customer's (or a customer merged into them) and in an issued state, apply the money to the invoice
/// (<c>ApplyPayment</c>) and write the append-only <c>Allocate</c> entry on the payment. Nothing is saved here -- every root
/// involved is tracked by the shared <c>DbContext</c> and committed together by <see cref="BillingPosting"/>.
/// </summary>
public static class PaymentAllocating
{
    public sealed record Outcome(IReadOnlyList<AppliedAllocation> Applied, IReadOnlyList<IPostingParticipant> Participants, CommandResponse? Violation);

    /// <param name="capAtOutstanding">Approval: each allocation is capped at what is still outstanding <b>now</b> and the remainder
    /// stays unallocated (U-D6). Otherwise an allocation above the outstanding is refused.</param>
    public static async Task<Outcome> ApplyAsync(
        IAggregateRootRepository<InvoiceAggregate> invoiceRepository, IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
        PaymentAggregate payment, IReadOnlyList<AllocationRequest> requests, bool capAtOutstanding, DateTimeOffset now, BillingActor actor)
    {
        var applied = new List<AppliedAllocation>();
        var participants = new List<IPostingParticipant>();

        // Several lines naming one invoice are one allocation.
        foreach (var request in requests.GroupBy(r => r.InvoiceId).Select(g => new AllocationRequest(g.Key, g.Sum(r => r.Amount))))
        {
            var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId);
            if (invoice is null)
                return Failed(BusinessViolationCodes.InvoiceNotFound, "Allocations", "An invoice was not found.");

            if (!await BelongsToCustomerAsync(customerRepository, invoice.CustomerId, payment.CustomerId))
                return Failed(BusinessViolationCodes.AllocationInvoiceCustomerMismatch, "Allocations", "A payment can only be applied to its own customer's invoices.");

            if (!InvoiceActionRules.CanApplyPayment(invoice.Status))
                return Failed(BusinessViolationCodes.InvoiceNotIssued, "Allocations", "A payment can only be applied to an invoice that is issued and not yet settled.");

            var amount = capAtOutstanding ? Math.Min(request.Amount, invoice.Outstanding) : request.Amount;
            if (amount <= 0m) continue;

            try
            {
                payment.Allocate(new PaymentAllocateDto(invoice.Id, amount, EarmarkMatching.Matches(payment.Earmark, invoice), now, actor));
                invoice.ApplyPayment(amount, actor);
            }
            catch (DomainRuleViolationException ex)
            {
                return Failed(ex.ErrorCode, ex.PropertyName, ex.Message);
            }

            applied.Add(new AppliedAllocation(invoice.Id, invoice.Number ?? string.Empty, amount, invoice.Outstanding));
            participants.Add(BillingPosting.Of(invoiceRepository, invoice));
        }

        return new Outcome(applied, participants, null);
    }

    private static Outcome Failed(string code, string property, string message) => new([], [], Violation(code, property, message));

    /// <summary>The invoice is the payer's own, or belongs to a customer that was merged into the payer (ADR-192).</summary>
    public static async Task<bool> BelongsToCustomerAsync(
        IAggregateRootRepository<CustomerAccountAggregate> customerRepository, Guid invoiceCustomerId, Guid paymentCustomerId)
    {
        if (invoiceCustomerId == paymentCustomerId) return true;

        var invoiceCustomer = await customerRepository.GetByIdAsync(invoiceCustomerId);
        return invoiceCustomer?.MergedIntoCustomerId == paymentCustomerId;
    }

    /// <summary>What the payment-received message says it went towards: the earmark label, the invoice numbers, or credit.</summary>
    public static string PurposeText(PaymentAggregate payment, IReadOnlyList<AppliedAllocation> applied)
    {
        if (payment.Earmark is { } earmark) return earmark.Label;
        if (applied.Count > 0) return string.Join(", ", applied.Select(a => a.Number));
        return "credit on your account";
    }
}
