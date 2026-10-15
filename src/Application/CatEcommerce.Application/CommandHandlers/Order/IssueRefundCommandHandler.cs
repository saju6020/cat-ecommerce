using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.10, §11.4 C25 (US-ORD-15, ADR-211, FR-BIL-5, BR-17) -- money back to the customer, recorded by a named person. Checks, in order: the method is allowed by how
/// the customer paid (<c>RefundMethodNotAllowed</c>); store credit is unavailable until Billing builds it (<c>RefundMethodUnavailable</c>, OQ-8); a cash refund names the receipt and
/// a transfer refund its reference, neither a card number (<c>RefundReferenceRequired</c>, C-5); the amount is no more than was paid and not yet refunded
/// (<c>RefundExceedsAmountPaid</c>). Then, in <b>one database transaction</b> (ADR-215), with an ADR-205 reconciliation issue if it fails after staff entered the money:
/// <list type="number">
/// <item>Billing's credit note: the one raised in full at cancellation is linked; otherwise a new one is issued for the refunded amount, spread over the invoice's remaining lines;</item>
/// <item>Billing's refund (money out), drawn from the order's payments newest first -- the credit note released their excess to the customer's credit;</item>
/// <item>the append-only <see cref="OrderRefundAggregate"/> (saved), the order's running <c>AmountRefunded</c> / <c>RefundDue</c> (tracked: a full refund of a delivered or returned order is
/// <c>Refunded</c>) and the customer's message (enlisted).</item>
/// </list>
/// </summary>
public sealed class IssueRefundCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<OrderRefundAggregate> refunds,
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IBillingRefundPlanner planner,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IReconciliationIssueRecorder reconciliation,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<Commands.Order.IssueRefundCommand>
{
    public async Task<CommandResponse> HandleAsync(Commands.Order.IssueRefundCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        var option = RefundMethodPolicy.Evaluate(order.Payments.Select(p => p.Method), storeCreditAvailable: false).Single(o => o.Method == command.Method);
        if (!option.AllowedForPayment)
            return Violation(BusinessViolationCodes.RefundMethodNotAllowed, nameof(command.Method), "That refund method is not available for how this order was paid.");
        if (!option.Available)
            return Violation(BusinessViolationCodes.RefundMethodUnavailable, nameof(command.Method), "Store credit is not available yet. Refund in cash at the shop or by bank transfer.");

        if (command.Method != OrderRefundMethod.StoreCredit && string.IsNullOrWhiteSpace(command.Reference))
            return Violation(BusinessViolationCodes.RefundReferenceRequired, nameof(command.Reference),
                command.Method == OrderRefundMethod.CashInStore ? "Enter the receipt number for the cash refund." : "Enter the bank transfer reference.");

        if (command.ReturnRequestId is { } returnId && (await returns.GetByIdAsync(returnId) is not { } linked || linked.OrderId != order.Id))
            return Violation(BusinessViolationCodes.ReturnNotFound, nameof(command.ReturnRequestId), "Return not found.");

        try
        {
            return await transaction.RunAsync(() => RefundAsync(command, order), response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        catch (Exception ex) when (!TransferConfirmation.IsBenign(ex))
        {
            await reconciliation.RecordAsync(new ReconciliationIssueDto(
                Guid.NewGuid(), order.Id, ReconciliationIssueKind.RefundFailed, new EnteredPaymentDetails(command.Method.DisplayName, command.Amount, command.Reference, null), order.CurrencyCode,
                $"{(ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex).GetType().Name}: the refund could not be recorded and was rolled back.",
                command.CorrelationId, command.UserContext!.UserId, timeProvider.GetUtcNow(), command.UserContext, order.BranchId));
            throw;
        }
    }

    private async Task<CommandResponse> RefundAsync(Commands.Order.IssueRefundCommand command, OrderAggregate order)
    {
        var now = timeProvider.GetUtcNow();
        var user = command.UserContext!;
        Guid? creditNoteId = null;

        if (command.Amount <= 0m || command.Amount > order.AmountPaid - order.AmountRefunded)
            return Violation(BusinessViolationCodes.RefundExceedsAmountPaid, nameof(command.Amount), "A refund cannot be more than what was paid and not yet refunded.");

        if (order.InvoiceId is { } invoiceId)
        {
            if (order.Status == OrderStatus.Cancelled && order.CreditNoteIds.Count > 0)
            {
                creditNoteId = order.CreditNoteIds[^1]; // the full credit note raised at cancellation covers it (US-ORD-15 AC2)
            }
            else
            {
                var plan = await planner.PlanCreditAsync(invoiceId, command.Amount); // Billing's published planning port (HO-BIL-001 §8.13)
                if (plan.IsRefused) return Violation(BusinessViolationCodes.RefundExceedsAmountPaid, nameof(command.Amount), plan.RefusalMessage!);

                var issued = await dispatcher.SendAsync(new IssueCreditNoteCommand
                {
                    CreditNoteId = Guid.NewGuid(), InvoiceId = invoiceId, BranchId = order.BranchId, Kind = command.ReturnRequestId is null ? "Goodwill" : "Return",
                    Reason = command.Reason, FullCredit = plan.Full, Lines = plan.Full ? null : plan.Lines.Select(l => new CreditLineRequest(l.InvoiceLineId, Amount: l.Amount)).ToList(), UserContext = user, CorrelationId = command.CorrelationId, ValidateCommand = true,
                });

                if (!issued.IsSuccess) return Surface(issued);
                creditNoteId = ((IssuedCreditNoteResult)issued.Result).CreditNoteId;
                order.AddCreditNote(creditNoteId.Value);
            }

            var draws = await planner.PlanRefundAsync(order.Payments.OrderByDescending(p => p.RecordedOn).Select(p => p.PaymentId).ToList(), command.Amount);
            foreach (var draw in draws.Draws)
            {
                var money = await dispatcher.SendAsync(new Commands.Billing.IssueRefundCommand
                {
                    RefundId = Guid.NewGuid(), PaymentId = draw.PaymentId, BranchId = order.BranchId, CreditNoteId = creditNoteId, Method = command.Method.DisplayName, Amount = draw.Amount,
                    Reference = command.Reference?.Trim(), Reason = command.Reason, UserContext = user, CorrelationId = command.CorrelationId, ValidateCommand = true,
                });

                if (!money.IsSuccess) return Surface(money);
            }

            if (draws.Shortfall > 0m)
                return Violation(BusinessViolationCodes.RefundExceedsAmountPaid, nameof(command.Amount), "The money to pay back is not available in the payments recorded for this order.");
        }

        var refund = new OrderRefundAggregate(command.RefundId);
        refund.Issue(order.Id, command.ReturnRequestId, command.Method, command.Amount, order.CurrencyCode, command.Reference, command.Reason, creditNoteId, now, user, command.CorrelationId);
        order.ApplyRefund(command.Amount, now, user, command.CorrelationId);
        OrderNotifications.EnqueueRefundIssued(outbox, links, order, command.Amount, command.Method.DisplayName, command.CorrelationId, user);

        await refunds.SaveAsync(refund, EventPublishOption.InMemoryPublishOnly); // commits the tracked order with it
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);  // dispatches the order's own events
        return Success(new IssueRefundResult(refund.Id, creditNoteId));
    }

    private static CommandResponse Surface(CommandResponse billing)
    {
        var first = billing.ValidationResult?.Errors.FirstOrDefault();
        return Violation(first?.ErrorCode ?? BusinessViolationCodes.RefundExceedsAmountPaid, first?.PropertyName ?? "Amount", first?.ErrorMessage ?? "Billing refused the refund.");
    }
}
