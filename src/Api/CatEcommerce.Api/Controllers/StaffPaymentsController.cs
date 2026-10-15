using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 §12.4 -- money received (US-BIL-11, 03, 04). A payment is recorded by a named person against a reference; there is
/// <b>no route that edits one</b> -- <c>PUT</c> and <c>PATCH</c> on a payment answer <c>405</c> (US-BIL-11 AC3): a mistake is reversed
/// and re-entered. The recording accepts an <c>Idempotency-Key</c> (ADR-187). Staff, branch-scoped.
/// </summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffPaymentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    private static readonly string[] NotFoundCodes = [BusinessViolationCodes.PaymentNotFound];

    [HttpPost("payments")]
    [RequireAction(Actions.Payments.Record)]
    public async Task<IActionResult> Record([FromBody] RecordPaymentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryReadIdempotencyKey(out var key, out var keyRefusal)) return keyRefusal!;

        var response = await dispatcher.SendAsync(new RecordPaymentCommand
        {
            PaymentId = Guid.NewGuid(), CustomerId = request.CustomerId, BranchId = branchId, Method = request.Method, Amount = request.Amount,
            ReceivedOn = request.ReceivedOn, ExternalReference = request.ExternalReference, Collector = request.Collector, Earmark = request.Earmark,
            Allocations = request.Allocations, Notes = request.Notes, IdempotencyKey = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (RecordPaymentResult)response.Result;
        var body = (await dispatcher.GetResultAsync(new GetPaymentRecordResultQuery
        { PaymentId = result.PaymentId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() })).Result;

        return result.Replayed ? Ok(body) : StatusCode(201, body);
    }

    [HttpPost("customers/{customerId:guid}/apply-credit")]
    [RequireAction(Actions.Payments.Allocate)]
    public async Task<IActionResult> ApplyCredit(Guid customerId, [FromBody] ApplyCustomerCreditRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApplyCustomerCreditCommand
        {
            CustomerId = customerId, InvoiceId = request.InvoiceId, BranchId = branchId, Amount = request.Amount,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);

        var result = (AppliedCustomerCreditResult)response.Result;
        return Ok(new
        {
            applied = result.Applied.Select(a => new { invoiceId = a.InvoiceId, number = a.Number, amount = a.Amount }),
            freeCreditRemaining = result.FreeCreditRemaining,
            invoiceOutstanding = result.InvoiceOutstanding,
        });
    }

    [HttpGet("payments")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId, [FromQuery] string? status, [FromQuery] string? method, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPaymentsQuery
        {
            BranchId = branchId, CustomerId = customerId, Status = status, Method = method, From = from, To = to, PageNumber = pageNumber, PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpGet("payments/export.csv")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? customerId, [FromQuery] string? status, [FromQuery] string? method, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPaymentsQuery
        {
            BranchId = branchId, CustomerId = customerId, Status = status, Method = method, From = from, To = to, ForExport = true,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        var csv = new StringBuilder();
        BillingCsv.Row(csv, "paymentId", "receiptNumber", "customerId", "customerName", "method", "status", "currency", "amount", "unallocated", "receivedOn", "recordedBy", "reference");
        foreach (var p in response.Result.Items)
            BillingCsv.Row(csv, BillingCsv.Text(p.PaymentId.ToString()), BillingCsv.Text(p.ReceiptNumber), BillingCsv.Text(p.CustomerId.ToString()), BillingCsv.Text(p.CustomerName),
                BillingCsv.Text(p.Method), BillingCsv.Text(p.Status), BillingCsv.Text(p.Amount.CurrencyCode), p.Amount.Amount, p.Unallocated.Amount,
                BillingCsv.Text(BillingCsv.Date(p.ReceivedOn)), BillingCsv.Text(p.RecordedByName), BillingCsv.Text(p.ExternalReference));

        Response.Headers.CacheControl = "no-store";
        return File(new UTF8Encoding(false).GetBytes(csv.ToString()), "text/csv; charset=utf-8", "payments.csv");
    }

    [HttpGet("payments/{id:guid}")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPaymentDetailQuery
        { PaymentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("payments/{id:guid}/receipt")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetReceipt(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPaymentReceiptQuery
        { PaymentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("approvals")]
    [RequireAction(Actions.Payments.Approve)]
    public async Task<IActionResult> GetApprovals()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetPendingApprovalsQuery { BranchId = branchId, CallerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("payments/{id:guid}/approve")]
    [RequireAction(Actions.Payments.Approve)]
    public async Task<IActionResult> Approve(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApprovePaymentCommand
        { PaymentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, NotFoundCodes);

        var result = (ApprovePaymentResult)response.Result;
        var currency = await CurrencyOfAsync(id, branchId);
        return Ok(new
        {
            applied = result.Applied.Select(a => new { invoiceId = a.InvoiceId, number = a.Number, amount = BillingMoney.Of(a.Amount, currency) }),
            leftUnallocated = BillingMoney.Of(result.LeftUnallocated, currency),
        });
    }

    [HttpPost("payments/{id:guid}/reject")]
    [RequireAction(Actions.Payments.Approve)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReasonRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RejectPaymentCommand
        { PaymentId = id, BranchId = branchId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, NotFoundCodes);
    }

    [HttpPost("payments/{id:guid}/allocations")]
    [RequireAction(Actions.Payments.Allocate)]
    public async Task<IActionResult> Allocate(Guid id, [FromBody] AllocatePaymentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AllocatePaymentCommand
        {
            PaymentId = id, BranchId = branchId, Allocations = request.Allocations,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, NotFoundCodes);

        var result = (AllocatePaymentResult)response.Result;
        var currency = await CurrencyOfAsync(id, branchId);
        return Ok(new
        {
            applied = result.Applied.Select(a => new { invoiceId = a.InvoiceId, number = a.Number, amount = BillingMoney.Of(a.Amount, currency), outstanding = BillingMoney.Of(a.OutstandingAfter, currency) }),
            unallocated = BillingMoney.Of(result.Unallocated, currency),
        });
    }

    [HttpPost("payments/{id:guid}/release-earmark")]
    [RequireAction(Actions.Payments.Allocate)]
    public async Task<IActionResult> ReleaseEarmark(Guid id, [FromBody] ReasonRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReleasePaymentEarmarkCommand
        { PaymentId = id, BranchId = branchId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, NotFoundCodes);
    }

    [HttpPost("payments/{id:guid}/reverse")]
    [RequireAction(Actions.Payments.Reverse)]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReasonRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReversePaymentCommand
        { PaymentId = id, BranchId = branchId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, NotFoundCodes);
    }

    private async Task<string> CurrencyOfAsync(Guid paymentId, Guid branchId)
    {
        var detail = await dispatcher.GetResultAsync(new GetPaymentDetailQuery
        { PaymentId = paymentId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return detail.Result?.Summary.Amount.CurrencyCode ?? "BDT";
    }
}
