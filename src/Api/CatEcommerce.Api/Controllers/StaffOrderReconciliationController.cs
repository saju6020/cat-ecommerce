using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
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

/// <summary>HO-ORD-001 §12.4 (US-ORD-08 AC2, ADR-205) -- the reconciliation issues accountants work through: payment details a staff member entered that a failed
/// recording would otherwise have lost. <c>Orders.Reconcile</c>. (Billing's own <c>StaffReconciliationController</c> reconciles couriers and the bank, US-BIL-06.)</summary>
[ApiController]
[Route("api/staff/orders/reconciliation-issues")]
public sealed class StaffOrderReconciliationController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    [HttpGet]
    [RequireAction(Actions.Orders.Reconcile)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!EnumQueryParameter.TryParse<ReconciliationIssueStatus>(status, out var parsed)) return BadRequest(new { error = "status must be Open or Resolved." });

        var query = new GetReconciliationIssuesQuery { BranchId = branchId, Status = parsed, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpPost("{issueId:guid}/resolve")]
    [RequireAction(Actions.Orders.Reconcile)]
    public async Task<IActionResult> Resolve(Guid issueId, [FromBody] ResolveReconciliationIssueRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ResolveReconciliationIssueCommand
        { IssueId = issueId, BranchId = branchId, Note = request.Note ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
