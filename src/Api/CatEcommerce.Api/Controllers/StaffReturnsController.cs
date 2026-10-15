using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Idempotency;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 (US-ORD-13, 14, 15) -- the staff side of returns and refunds. The branch is the active branch (<c>X-Branch-Id</c>); a return or an order of another branch does not
/// exist for the caller. Requesting, approving, rejecting and receiving are <c>Returns.Manage</c>; what happens to returned stock afterwards is Inventory's quarantine workflow, not this controller's (OQ-19);
/// refunds are <c>Refunds.Issue</c> and idempotent at the edge. Reads are <c>Orders.View</c>.
/// </summary>
[ApiController]
public sealed class StaffReturnsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    [HttpGet("api/staff/returns")]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!EnumQueryParameter.TryParse<ReturnStatus>(status, out var parsed)) return BadRequest(new { error = "status is not a return status." });

        var query = new GetReturnsQuery { BranchId = branchId, Status = parsed, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("api/staff/returns/{returnRequestId:guid}")]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> Get(Guid returnRequestId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetReturnDetailQuery { BranchId = branchId, ReturnRequestId = returnRequestId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-ORD-13 -- staff file a return on the customer's behalf.</summary>
    [HttpPost("api/staff/orders/{orderId:guid}/returns")]
    [RequireAction(Actions.Returns.Manage)]
    public async Task<IActionResult> Request(Guid orderId, [FromBody] RequestReturnRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!ReturnControllerSupport.TryMapLines(request.Lines, out var lines, out var error)) return BadRequest(new { error });

        var response = await dispatcher.SendAsync(new RequestReturnCommand
        {
            ReturnRequestId = Guid.NewGuid(), OrderId = orderId, BranchId = branchId, Lines = lines, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (RequestReturnResult)response.Result;
        return StatusCode(201, new { returnRequestId = result.ReturnRequestId, status = result.Status });
    }

    [HttpPost("api/staff/returns/{returnRequestId:guid}/approve")]
    [RequireAction(Actions.Returns.Manage)]
    public async Task<IActionResult> Approve(Guid returnRequestId, [FromBody] ApproveReturnRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApproveReturnCommand
        {
            ReturnRequestId = returnRequestId, BranchId = branchId, Note = request.Note, Lines = (request.Lines ?? []).Select(l => new ApprovedReturnLine(l.OrderLineId, l.DefectConfirmed, l.DefectNote)).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    [HttpPost("api/staff/returns/{returnRequestId:guid}/reject")]
    [RequireAction(Actions.Returns.Manage)]
    public async Task<IActionResult> Reject(Guid returnRequestId, [FromBody] RejectReturnRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RejectReturnCommand
        { ReturnRequestId = returnRequestId, BranchId = branchId, Note = request.Note ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-14 AC1 -- the goods arrived: into the branch's quarantine location, against the batches they were sold from.</summary>
    [HttpPost("api/staff/returns/{returnRequestId:guid}/receive")]
    [RequireAction(Actions.Returns.Manage)]
    public async Task<IActionResult> Receive(Guid returnRequestId, [FromBody] ReceiveReturnRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReceiveReturnCommand
        {
            ReturnRequestId = returnRequestId, BranchId = branchId, Lines = (request.Lines ?? []).Select(l => new ReceivedReturnLine(l.OrderLineId, l.QuantityReceived)).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    // ── refunds (Refunds.Issue) ─────────────────────────────────────────────────────────────────────
    [HttpGet("api/staff/orders/{orderId:guid}/refund-options")]
    [RequireAction(Actions.Refunds.Issue)]
    public async Task<IActionResult> RefundOptions(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetRefundOptionsQuery { BranchId = branchId, OrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-ORD-15 -- pay money back; records method, amount, reference, the staff member and the reason, links the Billing credit note and audits it.</summary>
    [HttpPost("api/staff/orders/{orderId:guid}/refunds")]
    [RequireAction(Actions.Refunds.Issue)]
    [Idempotent]
    public async Task<IActionResult> IssueRefund(Guid orderId, [FromBody] IssueRefundRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!EnumQueryParameter.TryParse<OrderRefundMethod>(request.Method, out var method) || method is null) return BadRequest(new { error = "method must be CashInStore, BankTransfer or StoreCredit." });

        var response = await dispatcher.SendAsync(new IssueRefundCommand
        {
            RefundId = Guid.NewGuid(), OrderId = orderId, BranchId = branchId, ReturnRequestId = request.ReturnRequestId, Method = method, Amount = request.Amount, Reference = request.Reference,
            Reason = request.Reason ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (IssueRefundResult)response.Result;
        return StatusCode(201, new { refundId = result.RefundId, creditNoteId = result.CreditNoteId });
    }
}
