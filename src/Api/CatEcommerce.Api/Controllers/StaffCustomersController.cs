using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-10 / US-ACC-11.</summary>
[ApiController]
[Route("api/staff/customers")]
public sealed class StaffCustomersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost]
    [RequireAction(Actions.Customers.RegisterWalkIn)]
    public async Task<IActionResult> RegisterWalkIn([FromBody] RegisterWalkInCustomerRequest request)
    {
        var customerId = Guid.NewGuid();
        var command = new RegisterWalkInCustomerCommand
        {
            CustomerId = customerId,
            FullName = request.FullName,
            MobileNumber = request.MobileNumber,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? StatusCode(201, new { customerId }) : BadRequest(response.ValidationResult);
    }

    [HttpGet]
    [RequireAction(Actions.Customers.View)]
    public async Task<IActionResult> Find([FromQuery] string? mobile, [FromQuery] string? email)
    {
        var response = await dispatcher.GetResultAsync(new FindCustomersQuery
        {
            Mobile = mobile,
            Email = email,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("duplicates")]
    [RequireAction(Actions.Customers.Merge)]
    public async Task<IActionResult> GetDuplicates([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        // Integrator fix 2026-09-26 (HO-ADM-001-SECURITY's carried line: "HO-ACC-001:
        // GET /api/staff-accounts?pageNumber=2147483647 -> 500 (the Skip overflow)"; this sibling
        // endpoint shares the exact same gap): (pageNumber-1)*pageSize can overflow int (Skip
        // overflow) for a very large caller-supplied pageNumber/pageSize -- the same class of gap
        // AdminLeaveRequestsController already closed with the shared PagingRules cap, applied here
        // the same way.
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var query = new GetDuplicateCustomerGroupsQuery
        {
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = pageSize.Value;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{customerId:guid}")]
    [RequireAction(Actions.Customers.View)]
    public async Task<IActionResult> GetCustomer(Guid customerId)
    {
        var response = await dispatcher.GetResultAsync(new GetStaffCustomerQuery
        {
            CustomerId = customerId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{duplicateId:guid}/merge")]
    [RequireAction(Actions.Customers.Merge)]
    public async Task<IActionResult> Merge(Guid duplicateId, [FromBody] MergeCustomerRequest request)
    {
        var command = new MergeCustomerCommand
        {
            DuplicateCustomerId = duplicateId,
            SurvivingCustomerId = request.SurvivingCustomerId,
            Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? Ok(response.Result) : BadRequest(response.ValidationResult);
    }
}
