using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 -- <c>GET /api/staff/customers/{customerId}/orders</c>, the orders panel of HO-ACC-002's customer page (HO-ORD-002 §11.14). Scoped to the staff
/// member's <b>active</b> branch like the rest of the staff order reads; an unknown customer is <c>404</c>.
/// </summary>
[ApiController]
[Route("api/staff/customers/{customerId:guid}/orders")]
public sealed class StaffCustomerOrdersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    [HttpGet]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> List(Guid customerId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var query = new GetStaffCustomerOrdersQuery { BranchId = branchId, CustomerId = customerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var result = (await dispatcher.GetResultAsync(query)).Result;
        return result is null ? NotFound() : Ok(result);
    }
}
