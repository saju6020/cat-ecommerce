using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>Who may act on which order: a customer on their own (by id and caller -- someone else's order does not exist for them), staff on the orders of
/// their active branch (HO-ADM-001 §8.2). Both answers are <c>OrderNotFound</c> (<c>404</c>, §12.3).</summary>
internal static class OrderAccess
{
    public static async Task<OrderAggregate?> ForCustomerAsync(IAggregateRootRepository<OrderAggregate> orders, Guid orderId, Guid customerId)
    {
        var order = await orders.GetByIdAsync(orderId);
        return order is not null && order.CustomerId == customerId ? order : null;
    }

    public static async Task<OrderAggregate?> ForBranchAsync(IAggregateRootRepository<OrderAggregate> orders, Guid orderId, Guid branchId)
    {
        var order = await orders.GetByIdAsync(orderId);
        return order is not null && order.BranchId == branchId ? order : null;
    }

    public static CommandResponse NotFound() => Violation(BusinessViolationCodes.OrderNotFound, "OrderId", "Order not found.");

    public static CommandResponse Stale() =>
        Violation(BusinessViolationCodes.StaleVersion, "ExpectedVersion", "This order has changed since you loaded it. Reload and try again.");

    /// <summary>The local calendar date at <paramref name="now"/> in a branch's zone.</summary>
    public static DateOnly LocalDate(DateTimeOffset now, string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);
}

/// <summary>HO-ORD-001 §11.4 C10 (FR-ORD-22) -- the customer's own reference for their transfer. Own order (<c>404</c> otherwise); the aggregate checks it still
/// awaits a transfer and that the text is not a card number (C-5).</summary>
public sealed class SubmitTransferReferenceCommandHandler(IAggregateRootRepository<OrderAggregate> orders, TimeProvider timeProvider)
    : ICommandHandlerAsync<SubmitTransferReferenceCommand>
{
    public async Task<CommandResponse> HandleAsync(SubmitTransferReferenceCommand command)
    {
        if (CartCaller.CustomerId(command) is not { } customerId) return OrderAccess.NotFound();

        var order = await OrderAccess.ForCustomerAsync(orders, command.OrderId, customerId);
        if (order is null) return OrderAccess.NotFound();

        try
        {
            order.SubmitTransferReference(command.Reference, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>What the two transfer handlers (C11, C12) share: confirm a verified transfer inside the caller's transaction.</summary>
public sealed record TransferDependencies(
    IAggregateRootRepository<OrderAggregate> Orders,
    IAggregateRootRepository<BranchAggregate> Branches,
    IBillableChargeRecorder Charges,
    IOrderBillingReader BillingReader,
    ITransactionalScope Transaction,
    IInMemoryDispatcher Dispatcher,
    INotificationOutbox Outbox,
    INotificationLinkBuilder Links,
    IReconciliationIssueRecorder Reconciliation,
    IBusinessSettings Settings,
    TimeProvider Time);

public static class TransferConfirmation
{
    /// <summary>
    /// The final branch of C11 (ADR-203, ADR-204, ADR-215), inside the caller's transaction. The order's charges are recorded and saved and Billing issues the
    /// invoice from them (once: an order that already has its invoice -- a held confirmation that was rejected and entered again -- reuses it). Then the payment
    /// is recorded in Billing <b>under the person who entered it, as themselves</b> (<c>BankTransfer</c>, the bank reference).
    /// <list type="bullet">
    /// <item><paramref name="hold"/> = false (at or below Billing's approval threshold): Billing accepts the payment at once and the order is confirmed, linked
    /// to the invoice, with the confirmation message enqueued.</item>
    /// <item><paramref name="hold"/> = true (above the threshold): Billing leaves the payment <c>PendingApproval</c> -- recorded by this person -- and the order
    /// keeps the hold, naming that payment. A <b>different</b> person approves it through Billing's published <c>ApprovePaymentCommand</c> (C12); Ordering never
    /// records a payment on anyone's behalf.</item>
    /// </list>
    /// One database transaction: it all happens or none of it does.
    /// </summary>
    public static async Task<CommandResponse> FinaliseAsync(
        TransferDependencies d, OrderAggregate order, decimal amount, string bankReference, DateTimeOffset receivedOn, Command origin, bool hold)
    {
        var branch = await d.Branches.GetByIdAsync(order.BranchId) ?? throw new BillingSeamException("The order's branch was not found.");
        var now = d.Time.GetUtcNow();
        var user = origin.UserContext!;

        var invoiceId = order.InvoiceId;
        if (invoiceId is null)
        {
            var deliveryTaxClass = order.Totals.DeliveryCharge > 0m ? (await d.BillingReader.GetDeliveryTaxAsync(now, default)).TaxClassId : null;

            await OrderBilling.RecordChargesAsync(d.Charges, OrderBilling.BuildCharges(order, deliveryTaxClass, OrderAccess.LocalDate(now, branch.TimeZoneId)));
            await d.Transaction.FlushAsync(); // Billing selects the charges from the database

            var invoice = await OrderBilling.IssueInvoiceAsync(d.Dispatcher, order, origin, pinnedDeliveryTaxClassId: deliveryTaxClass);
            order.AttachInvoice(invoice.InvoiceId, invoice.Number);
            invoiceId = invoice.InvoiceId;
        }

        var paymentId = await OrderBillingMoney.RecordPaymentAsync(
            d.Dispatcher, order, invoiceId.Value, "BankTransfer", amount, OrderAccess.LocalDate(receivedOn, branch.TimeZoneId), bankReference, null, user, origin.CorrelationId);

        if (hold)
        {
            order.HoldPaymentForApproval(new HeldPaymentConfirmation(amount, bankReference, receivedOn, user.UserId, now, paymentId), user, origin.CorrelationId);
            await d.Orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
            return Success(new ConfirmBankTransferResult(order.Status.DisplayName, true));
        }

        order.ConfirmPayment(paymentId, "BankTransfer", amount, now, user, origin.CorrelationId);
        OrderNotifications.EnqueueConfirmed(d.Outbox, d.Links, order, paid: true, origin.CorrelationId, user);

        await d.Orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success(new ConfirmBankTransferResult(order.Status.DisplayName, false));
    }

    /// <summary>The exception that actually failed: a nested dispatch wraps what a handler throws in a <c>TargetInvocationException</c>.</summary>
    public static Exception Innermost(Exception ex) => ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? Innermost(inner) : ex;

    /// <summary>True for the failures that need no reconciliation issue because no money movement could have been half-recorded: an optimistic-concurrency
    /// conflict (two staff acting at once, the worker expiring the order) or a cancelled request. The caller is told to reload (<c>409</c>).</summary>
    public static bool IsBenign(Exception ex) => Innermost(ex) is OperationCanceledException || IsConcurrencyConflict(Innermost(ex));

    /// <summary>EF Core's <c>DbUpdateConcurrencyException</c>, recognised by name because the Application layer does not reference EF Core (the host's ConcurrencyExceptionFilter maps it to 409).</summary>
    private static bool IsConcurrencyConflict(Exception ex) => ex.GetType().Name == "DbUpdateConcurrencyException";

    /// <summary>ADR-205: any exception other than a business refusal or a benign conflict after staff entered payment details leaves an <c>Open</c>
    /// reconciliation issue holding what was entered, in a fresh scope, and then propagates (a <c>500</c>-class answer naming the issue is the host's).</summary>
    public static async Task<Guid?> RaiseIssueAsync(
        TransferDependencies d, OrderAggregate order, ReconciliationIssueKind kind, decimal amount, string reference, DateTimeOffset receivedOn, Exception ex, Command origin)
    {
        if (IsBenign(ex)) return null;

        return await d.Reconciliation.RecordAsync(new ReconciliationIssueDto(
            Guid.NewGuid(), order.Id, kind, new EnteredPaymentDetails("BankTransfer", amount, reference, receivedOn), order.CurrencyCode,
            $"{Innermost(ex).GetType().Name}: the payment could not be recorded and was rolled back.", origin.CorrelationId, origin.UserContext!.UserId, d.Time.GetUtcNow(), origin.UserContext,
            order.BranchId));
    }
}

/// <summary>
/// HO-ORD-001 §11.4 C11 (US-ORD-19 AC2, US-ORD-08, FR-BIL-12) -- staff verified a transfer against the bank record. The order (of their active branch) must
/// still await a transfer, with no confirmation already waiting for a second person, and the entered amount must be <b>exactly</b> the total (OQ-4). Billing
/// records the payment under the person who entered it. At or below Billing's <c>billing.paymentApprovalThreshold</c> it is accepted and the order confirmed
/// at once; above it the payment waits in Billing for a second person and the order keeps the hold (ADR-204, owner decision 4 of 2026-10-09). ADR-205: a
/// failure after the entry leaves a reconciliation issue.
/// </summary>
public sealed class ConfirmBankTransferCommandHandler(TransferDependencies d) : ICommandHandlerAsync<ConfirmBankTransferCommand>
{
    public async Task<CommandResponse> HandleAsync(ConfirmBankTransferCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(d.Orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();
        if (order.Version != command.ExpectedVersion) return OrderAccess.Stale();

        try
        {
            order.EnsureCanConfirmTransfer(command.Amount, command.BankReference);

            var hold = command.Amount > d.Settings.GetDecimal(SettingKeys.BillingPaymentApprovalThreshold, order.BranchId);
            return await d.Transaction.RunAsync(
                () => TransferConfirmation.FinaliseAsync(d, order, command.Amount, command.BankReference.Trim(), command.ReceivedOn, command, hold),
                response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        catch (Exception ex)
        {
            await TransferConfirmation.RaiseIssueAsync(d, order, ReconciliationIssueKind.TransferConfirmationFailed, command.Amount, command.BankReference, command.ReceivedOn, ex, command);
            throw;
        }
    }
}

/// <summary>
/// C12 (ADR-204, FR-BIL-12, BR-17) -- a different staff member approves the held confirmation, <b>through Billing's published approval route</b>
/// (<c>ApprovePaymentCommand</c>, dispatched as the real approver; Billing itself refuses the person who recorded the payment). Then, in the same transaction,
/// the order is confirmed against that payment and the confirmation message is enqueued. Billing's own record shows recorded-by the first person and
/// approved-by the second (BR-17). Ordering never copies roles or builds an identity for anyone.
/// </summary>
public sealed class ApproveHeldPaymentCommandHandler(TransferDependencies d) : ICommandHandlerAsync<ApproveHeldPaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(ApproveHeldPaymentCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(d.Orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();
        if (order.Version != command.ExpectedVersion) return OrderAccess.Stale();

        HeldPaymentConfirmation held;
        try
        {
            held = order.RequireHeldPaymentForApprover(command.UserContext!.UserId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (held.PaymentId is not { } paymentId)
            return Violation(BusinessViolationCodes.NoPaymentAwaitingApproval, "OrderId", "This confirmation was held before payments were recorded at hold time. Reject it and confirm the transfer again.");

        try
        {
            return await d.Transaction.RunAsync(() => ApproveAsync(order, held, paymentId, command), r => r.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        catch (Exception ex)
        {
            await TransferConfirmation.RaiseIssueAsync(d, order, ReconciliationIssueKind.PaymentApprovalFailed, held.Amount, held.Reference, held.ReceivedOn, ex, command);
            throw;
        }
    }

    private async Task<CommandResponse> ApproveAsync(OrderAggregate order, HeldPaymentConfirmation held, Guid paymentId, ApproveHeldPaymentCommand command)
    {
        var refusal = await OrderBillingMoney.TryApprovePaymentAsync(d.Dispatcher, order, paymentId, command);
        if (refusal is not null) return refusal; // Billing's own refusal (the same person, a role it does not accept ...) -- a business answer, everything rolls back

        order.ConfirmPayment(paymentId, "BankTransfer", held.Amount, d.Time.GetUtcNow(), command.UserContext!, command.CorrelationId);
        OrderNotifications.EnqueueConfirmed(d.Outbox, d.Links, order, paid: true, command.CorrelationId, command.UserContext!);

        await d.Orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success(new ApproveHeldPaymentResult(order.Status.DisplayName));
    }
}

/// <summary>C12 -- the second person declines: Billing's pending payment is rejected through its published <c>RejectPaymentCommand</c> (as the real person, with the
/// note as the reason), the hold is cleared and the order is again awaiting a transfer confirmation. The invoice already issued for the order stays; the next
/// confirmation records a new payment against it.</summary>
public sealed class RejectHeldPaymentCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders, ITransactionalScope transaction, IInMemoryDispatcher dispatcher)
    : ICommandHandlerAsync<RejectHeldPaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectHeldPaymentCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        try
        {
            return await transaction.RunAsync(() => RejectAsync(order, command), r => r.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
    }

    private async Task<CommandResponse> RejectAsync(OrderAggregate order, RejectHeldPaymentCommand command)
    {
        var held = order.HeldPayment;
        order.RejectHeldPayment(command.UserContext!, command.CorrelationId);

        if (held?.PaymentId is { } paymentId)
        {
            var refusal = await OrderBillingMoney.TryRejectPaymentAsync(dispatcher, order, paymentId, command.Note, command);
            if (refusal is not null) return refusal;
        }

        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
