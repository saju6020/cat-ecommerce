using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.11, §11.4 C26 (US-ORD-16, ADR-212, ADR-215, ADR-216) -- the counter sale. The checks, in order: the customer's mobile is a valid number and, when an active customer already has it as their primary number, that customer is the buyer (found through HO-ACC-001's <c>FindCustomersQuery</c> -- a new buyer is registered, passwordless until they claim the account, through <c>RegisterWalkInCustomerCommand</c> in its own short transaction before the sale's transaction opens and before any business check, so a refused sale still leaves the buyer stored -- FU1 D2; a Merged holder of the mobile is followed to its surviving account -- FU1 D1: <see cref="ICounterBuyerRegistrar"/>); the lines are priced by the same
/// <see cref="CheckoutPricing"/> an online order uses, with no delivery; a prescription food needs a customer and a cat (<c>PrescriptionCatRequired</c>); every scanned line
/// must be sellable in the quantity asked (a counter sale is never silently reduced); the tenders cover the total exactly (<see cref="TenderCalculator"/>). Then, in
/// <b>one database transaction</b>: the order is placed, the stock <b>deducted</b> FEFO through <see cref="IStockLedger"/> (the same ledger as online orders), the discount use
/// counted, the charges recorded and saved, the invoice issued by Billing, one payment recorded per tender, and the order finished
/// (<c>PendingPayment -> Confirmed -> Delivered</c>). A failure after the cashier took money rolls everything back and leaves an ADR-205 reconciliation issue naming what was entered.
/// </summary>
public sealed class CompleteCounterSaleCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<DiscountCodeAggregate> discountCodes,
    IAggregateRootRepository<BranchAggregate> branches,
    ICheckoutInputsLoader inputsLoader,
    ICounterCustomerFinder customerFinder,
    ICounterBuyerRegistrar buyerRegistrar,
    IOrderNumberGenerator numbers,
    IStockLedger stock,
    IBillableChargeRecorder charges,
    IReconciliationIssueRecorder reconciliation,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CompleteCounterSaleCommand>
{
    public async Task<CommandResponse> HandleAsync(CompleteCounterSaleCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var branch = await branches.GetByIdAsync(command.BranchId);
        if (branch is not { IsActive: true }) return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        string mobile;
        try
        {
            mobile = MobileNumber.Create(command.CustomerMobile).Value;
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, nameof(command.CustomerMobile), ex.Message);
        }

        // ADR-216 (amended 2026-10-10, ORD 42/43; FU1 D2): the buyer is found or registered in its own short transaction BEFORE any business check and before the
        // sale's scope opens. A shape-valid name + mobile therefore always leaves the walk-in stored, even when the sale is then refused (stock, prescription, tenders...);
        // the account intentionally outlives a refused sale and the retry reuses it. A lost race on the unique mobile cannot abort the sale. FU1 D1: a mobile held by a
        // Merged account resolves to the surviving account; one held by an account whose survivor is not active is a clear cashier-facing refusal.
        var customer = await customerFinder.FindActiveByPrimaryMobileAsync(mobile, default);
        if (customer is null)
        {
            var resolution = await buyerRegistrar.FindOrRegisterAsync(mobile, command.CustomerName, command.UserContext!, command.CorrelationId, default);
            if (resolution.Buyer is null) return Violation(resolution.RefusalCode!, resolution.RefusalProperty!, resolution.RefusalMessage!);
            customer = resolution.Buyer;
        }

        var inputs = await inputsLoader.LoadCounterAsync(
            branch.Id, customer.CustomerId, command.Lines.Select(l => new CounterLineRequest(l.SkuId, l.Quantity, l.CatId)).ToList(), command.DiscountCode, now, default);
        var quote = CheckoutPricing.Quote(inputs);

        // The prescription refusal is the one the cashier can act on (add the customer and cat); it outranks "nothing to sell" when that line was the only one.
        if (quote.BlockingReasons.Contains(BusinessViolationCodes.PrescriptionCatRequired))
            return Violation(BusinessViolationCodes.PrescriptionCatRequired, "Lines", OrderMessages.PrescriptionCatRequired);

        if (!quote.CanPlace) return OrderPlacement.RefusalFor(quote);
        if (CounterRefusal(command, inputs, quote) is { } refusal) return refusal;

        var tenders = TenderCalculator.Evaluate(
            quote.Totals.GrandTotal, command.Tenders.Select(t => new CounterTender(t.Method, MinorUnits.FromAmount(t.Amount), t.Reference)).ToList());
        if (!tenders.IsSuccess) return Violation(tenders.ViolationCode!, "Tenders", tenders.Message!);

        foreach (var tender in command.Tenders)
        {
            try { OrderFreeText.RefuseCardNumber(tender.Reference, "Tenders"); }
            catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message); }
        }


        try
        {
            return await transaction.RunAsync(() => SellAsync(command, now, inputs, quote, tenders, customer, branch), response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        catch (Exception ex) when (!TransferConfirmation.IsBenign(ex))
        {
            var first = command.Tenders[0];
            await reconciliation.RecordAsync(new ReconciliationIssueDto(
                Guid.NewGuid(), null, ReconciliationIssueKind.CounterSaleFailed,
                new EnteredPaymentDetails(string.Join("+", command.Tenders.Select(t => t.Method.DisplayName).Distinct()), command.Tenders.Sum(t => t.Amount), first.Reference, now),
                quote.CurrencyCode,
                $"{(ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex).GetType().Name}: the counter sale could not be recorded and was rolled back.",
                command.CorrelationId, command.UserContext!.UserId, now, command.UserContext, command.BranchId));
            throw;
        }
    }

    /// <summary>A counter sale is exactly what was scanned: a line the quote left out, or cut down, is a refusal (the cashier sees why and rescans).</summary>
    private static CommandResponse? CounterRefusal(CompleteCounterSaleCommand command, CheckoutInputs inputs, CheckoutQuote quote)
    {
        if (inputs.Lines.Count != command.Lines.Count)
            return Violation(BusinessViolationCodes.SkuUnavailable, "Lines", "One of the items is not in the catalogue.");

        if (quote.ExcludedLines.FirstOrDefault() is { } excluded)
        {
            var code = excluded.ReasonCode == nameof(CheckoutChangeKind.PrescriptionAuthorisationExpired) ? BusinessViolationCodes.PrescriptionNotAuthorised : BusinessViolationCodes.SkuUnavailable;
            var message = quote.Changes.FirstOrDefault(c => c.LineRef == excluded.LineRef)?.Message ?? $"'{excluded.ProductName}' cannot be sold.";
            return Violation(code, "Lines", message);
        }

        if (quote.Changes.FirstOrDefault(c => c.Kind == CheckoutChangeKind.QuantityReduced) is { } reduced)
            return Violation(BusinessViolationCodes.StockNoLongerAvailable, "Lines", reduced.Message);

        return null;
    }

    private async Task<CommandResponse> SellAsync(
        CompleteCounterSaleCommand command, DateTimeOffset now, CheckoutInputs inputs, CheckoutQuote quote, TenderOutcome tenders, CustomerContactFacts buyer, BranchAggregate branch)
    {
        var user = command.UserContext!;
        var appliedDiscount = quote.Discount is { Applied: true } && inputs.Discount is not null ? inputs.Discount : null;

        var billingCustomerId = buyer.CustomerId;

        var order = new OrderAggregate(command.OrderId);
        order.Place(new PlaceOrderDto(
            await numbers.NextAsync(now), OrderChannel.Counter, buyer.CustomerId, branch.Id, FulfilmentMethod.Counter, OrderPaymentMethod.InStore, quote.CurrencyCode,
            new FulfilmentDetails(buyer.FullName, buyer.Mobile, null, null, null, null, null, null, null, null, null, null, null, null, null, null),
            quote.Lines.ToDomainDtos(), quote.Totals.ToOrderTotals(), appliedDiscount?.DiscountCodeId, appliedDiscount?.Code, quote.Fingerprint, command.IdempotencyKey, now, user,
            command.CorrelationId));

        // ── stock: the goods leave the shelf now, FEFO, through the one ledger ──
        var taken = await OrderStock.DeductAsync(stock, transaction, order);
        if (!taken.IsSuccess) return Violation(taken.ViolationCode!, taken.PropertyName!, taken.Message!);
        order.RecordAllocations(taken.Allocations);

        if (appliedDiscount is not null)
        {
            var code = await discountCodes.GetByIdAsync(appliedDiscount.DiscountCodeId);
            if (code is null) return Violation(BusinessViolationCodes.CheckoutQuoteChanged, nameof(command.DiscountCode), OrderMessages.CheckoutQuoteChanged);
            code.Redeem();
        }

        // ── Billing: charges (enlisted), one save, then the invoice and a payment per tender ──
        var serviceDate = OrderAccess.LocalDate(now, branch.TimeZoneId);
        await OrderBilling.RecordChargesAsync(charges, OrderBilling.BuildCharges(order, null, serviceDate, billingCustomerId));
        await orders.SaveAsync(order, EventPublishOption.InMemoryPublishOnly);

        var invoice = await OrderBilling.IssueInvoiceAsync(dispatcher, order, command, billingCustomerId);
        order.AttachInvoice(invoice.InvoiceId, invoice.Number);

        foreach (var payment in tenders.Payments)
        {
            var method = payment.Method == TenderMethod.InStoreCard ? "CardInStore" : "CashInStore";
            var paymentId = await OrderBillingMoney.RecordPaymentAsync(
                dispatcher, order, invoice.InvoiceId, method, payment.Amount.ToAmount(), serviceDate, payment.Reference, null, user, command.CorrelationId, billingCustomerId, approvalWaiverReason: ApprovalWaiverReasons.CounterTender);
            order.RecordPayment(paymentId, method, payment.Amount.ToAmount(), now);
        }

        order.CompleteCounterSale(tenders.ChangeDue.ToAmount(), now, user, command.CorrelationId);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);

        return Success(new CounterSaleResult(order.Id, order.OrderNumber, invoice.Number, order.Totals.GrandTotal, tenders.ChangeDue.ToAmount(), order.CurrencyCode));
    }
}
