using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>Billing did not do what a placement needed (a refusal, or a missing prerequisite such as the branch's billing profile). A technical or
/// configuration failure, never a customer's mistake: the unit of work rolls back and the host answers <c>500</c>. Carries the Billing violation codes
/// only -- never a message or any data -- so it is safe to log (HO-ORD-001 §8.16, ADR-205).</summary>
public sealed class BillingSeamException(string message) : InvalidOperationException(message);

/// <summary>
/// HO-ORD-001 §8.14, ADR-201, ADR-215 -- Ordering's use of the Billing contract <b>as HO-BIL-001 published it</b> (§8.13): the order's lines (and its
/// delivery charge) are recorded as <i>charges</i> through the enlisting <see cref="IBillableChargeRecorder"/>, in the placement's own save; the invoice is
/// then issued by the published <see cref="IssueInvoiceFromChargesCommand"/> with the order as the <c>Source</c> selector, inside the same database
/// transaction (<see cref="ITransactionalScope"/>). Prices, discounts and tax classes are the order's snapshot -- Billing validates and resolves the rate
/// at issue, it never reprices. Every order discount share is folded into its line's <c>LineDiscount</c> (the producer apportions, §8.3), so Billing's
/// per-line tax on <c>quantity x unit - discount</c> equals the order's own.
/// </summary>
public static class OrderBilling
{
    public const string DeliveryLineRef = "delivery";

    public static IReadOnlyList<ChargeRequestDto> BuildCharges(OrderAggregate order, Guid? deliveryTaxClassId, DateOnly serviceDate, Guid? billingCustomerId = null)
    {
        var customerId = billingCustomerId ?? order.CustomerId ?? throw new BillingSeamException("An order without a customer cannot be billed here.");
        var currency = order.CurrencyCode;
        var charges = new List<ChargeRequestDto>();

        foreach (var line in order.Lines)
        {
            var description = string.IsNullOrWhiteSpace(line.VariantLabel) ? line.ProductName : $"{line.ProductName} - {line.VariantLabel}";
            charges.Add(new ChargeRequestDto(
                customerId, order.BranchId, line.CatId, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, order.Id, line.Id.ToString("N"),
                null, null, description.Length > 300 ? description[..300] : description, line.Quantity, new Money(line.UnitPrice, currency),
                new Money(line.LineDiscount + line.OrderDiscountShare, currency), line.TaxClassId, serviceDate));
        }

        if (order.Totals.DeliveryCharge > 0m)
        {
            // FU1 D3 (owner decision 2026-10-10): the delivery tax is configured (default 0 %), never a customer-facing refusal. The DELIVERY tax class is seeded; its
            // absence (deleted or deactivated, with no other 0 % class) is a configuration failure -- a 5xx, nothing written, no data in the message.
            var taxClassId = deliveryTaxClassId ?? throw new BillingSeamException("The shop has no tax class for the delivery charge (DELIVERY).");

            charges.Add(new ChargeRequestDto(
                customerId, order.BranchId, null, BusinessLine.Shop, ItemKind.Fee, ChargeSourceKind.Order, order.Id, DeliveryLineRef,
                null, null, "Delivery charge", 1m, new Money(order.Totals.DeliveryCharge, currency), Money.Zero(currency), taxClassId, serviceDate));
        }

        return charges;
    }

    /// <summary>Records the order's charges in the caller's unit of work (no save).</summary>
    public static async Task RecordChargesAsync(IBillableChargeRecorder recorder, IReadOnlyList<ChargeRequestDto> charges, CancellationToken ct = default)
    {
        foreach (var charge in charges) await recorder.RecordAsync(charge, ct);
    }

    /// <summary>
    /// FU1 ORD-56: <paramref name="pinnedDeliveryTaxClassId"/> reads that class's rate as of the order's placement (a held transfer is invoiced at the delivery tax it was quoted).
    /// Issues the invoice for the order's recorded charges (<c>Source = Order, orderId</c>). The charges must already be <b>saved</b> in this transaction --
    /// Billing selects them from the database. A refusal is a <see cref="BillingSeamException"/> carrying Billing's codes.
    /// </summary>
    public static async Task<IssuedInvoiceResult> IssueInvoiceAsync(IInMemoryDispatcher dispatcher, OrderAggregate order, Command origin, Guid? billingCustomerId = null, Guid? pinnedDeliveryTaxClassId = null)
    {
        var response = await dispatcher.SendAsync(new IssueInvoiceFromChargesCommand
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = billingCustomerId ?? order.CustomerId ?? throw new BillingSeamException("An order without a customer cannot be invoiced here."),
            BranchId = order.BranchId,
            Selector = new InvoiceChargeSelector(Source: new SourceChargeSelector(ChargeSourceKind.Order.DisplayName, order.Id)),
            IdempotencyKey = $"order-{order.Id:N}",
            PinnedTaxClassId = pinnedDeliveryTaxClassId,
            PinnedTaxRateAsOf = pinnedDeliveryTaxClassId is null ? null : order.PlacedOn,
            UserContext = origin.UserContext,
            CorrelationId = origin.CorrelationId,
            ValidateCommand = true,
        });

        if (!response.IsSuccess || response.Result is not IssuedInvoiceResult issued)
            throw new BillingSeamException("Billing refused the invoice for the order: " + Codes(response));

        return issued;
    }

    public static string Codes(CommandResponse response) =>
        string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []);
}

/// <summary>Billing's payment and credit-note commands, used the way the published contract (HO-BIL-001 §8.13) allows: dispatched in-process, inside the
/// caller's database transaction. A refusal is a <see cref="BillingSeamException"/>.</summary>
public static class OrderBillingMoney
{
    /// <summary>Records money received against the order's invoice. Above Billing's approval threshold a bank-transfer payment is <c>Pending</c> until a different
    /// person approves it; cash on delivery and counter tenders pass an <paramref name="approvalWaiverReason"/> (FU1 D4, OQ-7) and are accepted at once, whatever their size.</summary>
    public static async Task<Guid> RecordPaymentAsync(
        IInMemoryDispatcher dispatcher, OrderAggregate order, Guid invoiceId, string billingMethod, decimal amount, DateOnly receivedOn, string? reference,
        PaymentCollectorInput? collector, Platform.Infrastructure.Common.Security.UserContext recordedBy, Guid correlationId, Guid? billingCustomerId = null,
        string? approvalWaiverReason = null)
    {
        var paymentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RecordPaymentCommand
        {
            PaymentId = paymentId,
            CustomerId = billingCustomerId ?? order.CustomerId ?? throw new BillingSeamException("An order without a customer cannot record a payment here."),
            BranchId = order.BranchId,
            Method = billingMethod,
            Amount = amount,
            ReceivedOn = receivedOn,
            ExternalReference = reference,
            Collector = collector,
            Allocations = [new PaymentAllocationInput(invoiceId, amount)],
            UserContext = recordedBy,
            CorrelationId = correlationId,
            ApprovalWaiverReason = approvalWaiverReason,
            ValidateCommand = true,
        });

        if (!response.IsSuccess) throw new BillingSeamException("Billing refused the payment for the order: " + OrderBilling.Codes(response));
        return ((RecordPaymentResult)response.Result).PaymentId;
    }

    /// <summary>
    /// Approves a pending Billing payment through Billing's published <see cref="ApprovePaymentCommand"/>, dispatched <b>as the real approver</b> (the caller's own
    /// <c>UserContext</c>, untouched). Billing enforces "a different person from the recorder". Returns Billing's refusal as a business answer, or null on success.
    /// </summary>
    public static async Task<CommandResponse?> TryApprovePaymentAsync(IInMemoryDispatcher dispatcher, OrderAggregate order, Guid paymentId, Command approver)
    {
        var response = await dispatcher.SendAsync(new ApprovePaymentCommand
        {
            PaymentId = paymentId, BranchId = order.BranchId, UserContext = approver.UserContext, CorrelationId = approver.CorrelationId, ValidateCommand = true,
        });

        return response.IsSuccess ? null : Surface(response, "Billing refused the payment approval.");
    }

    /// <summary>Rejects a pending Billing payment through Billing's published <see cref="RejectPaymentCommand"/> as the real person, with the reason. Null on success.</summary>
    public static async Task<CommandResponse?> TryRejectPaymentAsync(IInMemoryDispatcher dispatcher, OrderAggregate order, Guid paymentId, string reason, Command rejecter)
    {
        var response = await dispatcher.SendAsync(new RejectPaymentCommand
        {
            PaymentId = paymentId, BranchId = order.BranchId, Reason = reason, UserContext = rejecter.UserContext, CorrelationId = rejecter.CorrelationId, ValidateCommand = true,
        });

        return response.IsSuccess ? null : Surface(response, "Billing refused to reject the payment.");
    }

    private static CommandResponse Surface(CommandResponse billing, string fallback)
    {
        var first = billing.ValidationResult?.Errors.FirstOrDefault();
        return OrderHandlerSupport.Violation(first?.ErrorCode ?? "BillingRefused", first?.PropertyName ?? "PaymentId", first?.ErrorMessage ?? fallback);
    }

    /// <summary>A full credit note against the order's invoice (cancellation, delivery failure). Returns the credit note's id.</summary>
    public static async Task<Guid> IssueFullCreditNoteAsync(IInMemoryDispatcher dispatcher, OrderAggregate order, Guid invoiceId, string kind, string reason, Command origin)
    {
        var creditNoteId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new IssueCreditNoteCommand
        {
            CreditNoteId = creditNoteId, InvoiceId = invoiceId, BranchId = order.BranchId, Kind = kind, Reason = reason, FullCredit = true,
            UserContext = origin.UserContext, CorrelationId = origin.CorrelationId, ValidateCommand = true,
        });

        if (!response.IsSuccess) throw new BillingSeamException("Billing refused the credit note for the order: " + OrderBilling.Codes(response));
        return ((IssuedCreditNoteResult)response.Result).CreditNoteId;
    }
}
