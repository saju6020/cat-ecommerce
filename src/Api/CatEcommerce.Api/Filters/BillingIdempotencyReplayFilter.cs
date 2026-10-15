using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-BIL-001 §12.5 / ADR-187 -- <b>idempotency at the HTTP edge for the money-moving writes</b>. The handlers look the
/// <c>(caller, Idempotency-Key)</c> pair up first, and a repeat arriving after the first finished returns the stored result
/// without ever reaching the database constraint. This filter answers the <i>parallel</i> double-submit: two identical requests that
/// both passed the pre-check before either committed; the loser's insert hits the unique <c>ix_*_idempotency</c> index and -- instead
/// of an error -- receives the original result with <c>200</c> ("a double-clicked Record payment must not record twice"). Any other
/// constraint is left to <see cref="UniqueConstraintExceptionFilter"/> or rethrown: an unexpected <c>23505</c> is a bug.
/// </summary>
public sealed class BillingIdempotencyReplayFilter : IAsyncExceptionFilter
{
    public const string PaymentIndex = "ix_payment_idempotency";
    public const string InvoiceIndex = "ix_invoice_idempotency";
    public const string CreditNoteIndex = "ix_credit_note_idempotency";
    public const string RefundIndex = "ix_refund_idempotency";

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var pg = Unwrap(context.Exception);
        if (pg is not { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } constraint }) return;
        if (constraint is not (PaymentIndex or InvoiceIndex or CreditNoteIndex or RefundIndex)) return;

        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values.ToString())) return;

        var key = values.ToString().Trim();
        var services = http.RequestServices;
        var caller = services.GetRequiredService<IUserContextProvider>().GetUserContext().UserId;
        var finder = services.GetRequiredService<IBillingFinder>();

        switch (constraint)
        {
            case PaymentIndex:
                var payment = await finder.FindPaymentByIdempotencyKeyAsync(caller, key);
                if (payment is null) return;

                var dispatcher = services.GetRequiredService<IInMemoryDispatcher>();
                var stored = await dispatcher.GetResultAsync(new GetPaymentRecordResultQuery
                { PaymentId = payment.PaymentId, UserContext = services.GetRequiredService<IUserContextProvider>().GetUserContext(), CorrelationId = Guid.NewGuid() });
                if (stored.Result is null) return;

                context.Result = new OkObjectResult(stored.Result);
                break;

            case InvoiceIndex:
                var invoice = await finder.FindInvoiceByIdempotencyKeyAsync(caller, key);
                if (invoice is null) return;

                context.Result = new OkObjectResult(new { invoiceId = invoice.InvoiceId, number = invoice.Number });
                break;

            case CreditNoteIndex:
                var creditNote = await finder.FindCreditNoteByIdempotencyKeyAsync(caller, key);
                if (creditNote is null) return;

                context.Result = new OkObjectResult(new { creditNoteId = creditNote.CreditNoteId, number = creditNote.Number, releasedToCustomerCredit = creditNote.ReleasedToCustomerCredit });
                break;

            case RefundIndex:
                var refund = await finder.FindRefundByIdempotencyKeyAsync(caller, key);
                if (refund is null) return;

                context.Result = new OkObjectResult(new { refundId = refund.RefundId });
                break;

            default:
                return;
        }

        context.ExceptionHandled = true;
    }

    private static PostgresException? Unwrap(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateException { InnerException: PostgresException pg }) return pg;
        }

        return null;
    }
}
