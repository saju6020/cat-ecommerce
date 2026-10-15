using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BIL-001 section 12.4 (FR-BIL-5, FR-ORD-15 server half) -- money paid back, drawn from an accepted payment unallocated
/// amount. The record names method, amount, reference, who and why. Staff, branch-scoped; accepts an <c>Idempotency-Key</c> (ADR-187).</summary>
[ApiController]
[Route("api/staff/billing/refunds")]
public sealed class StaffRefundsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpPost]
    [RequireAction(Actions.Refunds.Issue)]
    public async Task<IActionResult> Issue([FromBody] IssueRefundRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryReadIdempotencyKey(out var key, out var keyRefusal)) return keyRefusal!;

        var response = await dispatcher.SendAsync(new IssueRefundCommand
        {
            RefundId = Guid.NewGuid(), PaymentId = request.PaymentId, BranchId = branchId, CreditNoteId = request.CreditNoteId, Method = request.Method,
            Amount = request.Amount, Reference = request.Reference, Reason = request.Reason, IdempotencyKey = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.PaymentNotFound);

        var result = (IssuedRefundResult)response.Result;
        var body = new { refundId = result.RefundId };
        return result.Replayed ? Ok(body) : StatusCode(201, body);
    }

    [HttpGet]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetRefundsQuery
        {
            BranchId = branchId, From = from, To = to, PageNumber = pageNumber, PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }
}
