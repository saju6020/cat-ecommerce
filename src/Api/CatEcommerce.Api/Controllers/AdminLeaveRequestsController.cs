using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- administrator leave decisions.</summary>
[ApiController]
[Route("api/admin/leave-requests")]
public sealed class AdminLeaveRequestsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Leave.Decide)]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] Guid? staffAccountId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!EnumQueryParameter.TryParse<LeaveStatus>(status, out var parsedStatus))
            return BadRequest(new { error = $"'{status}' is not a valid leave status." });
        // Gate fix 2026-09-26 (round 4): (pageNumber-1)*pageSize can overflow int (Skip overflow),
        // the same class of gap already fixed on audit search -- same shared cap, PagingRules.
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        // Gate fix 2026-09-26 (CR LOW, hardening round): pageNumber alone was capped, but pageSize
        // was not -- pageSize=1_500_000_000 with pageNumber=3 still overflowed Skip negative.
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var query = new GetLeaveRequestsQuery
        {
            // Gate fix 2026-09-26: pass the canonical DisplayName on, not the caller's raw casing.
            Status = parsedStatus?.DisplayName, StaffAccountId = staffAccountId, From = from, To = to,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = pageSize.Value;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Leave.Decide)]
    public async Task<IActionResult> RequestOnBehalf([FromBody] RequestLeaveRequest request)
    {
        var leaveRequestId = Guid.NewGuid();
        var command = new RequestLeaveCommand
        {
            LeaveRequestId = leaveRequestId, StaffAccountId = request.StaffAccountId, StartsOn = request.StartsOn, EndsOn = request.EndsOn,
            Kind = request.Kind, Note = request.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { leaveRequestId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{id:guid}/approve")]
    [RequireAction(Actions.Leave.Decide)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] LeaveDecisionRequest request)
    {
        var command = new ApproveLeaveCommand
        {
            LeaveRequestId = id, DecisionNote = request.DecisionNote,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{id:guid}/reject")]
    [RequireAction(Actions.Leave.Decide)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] LeaveDecisionRequest request)
    {
        var command = new RejectLeaveCommand
        {
            LeaveRequestId = id, DecisionNote = request.DecisionNote,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Leave.Decide)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var command = new CancelLeaveCommand
        {
            LeaveRequestId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
