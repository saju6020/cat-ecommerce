using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BIL-001 §12.4 -- what customers owe (US-BIL-03): the per-customer balance (<c>StaffBillingCustomers</c>) and the ageing
/// view (<c>StaffReceivables</c>).</summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffReceivablesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    /// <summary>US-BIL-03 AC1 -- the outstanding balance, overdue, free and earmarked credit and unbilled charges, for the customer
    /// profile screen. The customer set includes anyone merged into them (ADR-192).</summary>
    [HttpGet("customers/{customerId:guid}/summary")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetCustomerSummary(Guid customerId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCustomerBillingSummaryQuery
        { CustomerId = customerId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    /// <summary>US-BIL-03 AC2 -- outstanding per issued invoice bucketed by days overdue, per customer.</summary>
    [HttpGet("receivables/ageing")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetAgeing([FromQuery] DateOnly? asOf, [FromQuery] Guid? customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetAgeingQuery
        {
            BranchId = branchId, AsOf = asOf, CustomerId = customerId, PageNumber = pageNumber, PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }
}
