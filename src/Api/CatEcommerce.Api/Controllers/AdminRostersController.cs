using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- rosters, roster exceptions, availability.</summary>
[ApiController]
[Route("api/admin/branches/{branchId:guid}")]
public sealed class AdminRostersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("rosters")]
    [RequireAction(Actions.Rosters.View)]
    public async Task<IActionResult> GetRosters(Guid branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetBranchRostersQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPut("rosters/{staffAccountId:guid}/pattern")]
    [RequireAction(Actions.Rosters.Manage)]
    public async Task<IActionResult> SetPattern(Guid branchId, Guid staffAccountId, [FromBody] SetRosterPatternRequest request)
    {
        var slots = new List<ShiftSlotInput>();
        foreach (var s in request.Slots)
        {
            if (!Enum.TryParse<DayOfWeek>(s.DayOfWeek, true, out var day)) return BadRequest(new { error = $"'{s.DayOfWeek}' is not a valid day." });
            if (!TimeOnly.TryParseExact(s.Starts, "HH:mm", out var starts)) return BadRequest(new { error = "Invalid starts." });
            if (!TimeOnly.TryParseExact(s.Ends, "HH:mm", out var ends)) return BadRequest(new { error = "Invalid ends." });
            slots.Add(new ShiftSlotInput(day, starts, ends, s.DefaultRoomId));
        }

        var command = new SetRosterPatternCommand
        {
            StaffAccountId = staffAccountId, BranchId = branchId, Slots = slots, EffectiveFrom = request.EffectiveFrom,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpDelete("rosters/{staffAccountId:guid}/next-pattern")]
    [RequireAction(Actions.Rosters.Manage)]
    public async Task<IActionResult> CancelNextPattern(Guid branchId, Guid staffAccountId)
    {
        var command = new CancelScheduledRosterPatternCommand
        {
            StaffAccountId = staffAccountId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("roster-exceptions")]
    [RequireAction(Actions.Rosters.View)]
    public async Task<IActionResult> GetExceptions(Guid branchId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var response = await dispatcher.GetResultAsync(new GetRosterExceptionsQuery { BranchId = branchId, From = from, To = to, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        TruncationHeader.ApplyIfAtCap(Response, response.Result.Count);
        return Ok(response.Result);
    }

    [HttpPost("roster-exceptions")]
    [RequireAction(Actions.Rosters.Manage)]
    public async Task<IActionResult> CreateException(Guid branchId, [FromBody] CreateRosterExceptionRequest request, [FromQuery] Guid staffAccountId)
    {
        if (!TimeOnly.TryParseExact(request.Starts, "HH:mm", out var starts)) return BadRequest(new { error = "Invalid starts." });
        if (!TimeOnly.TryParseExact(request.Ends, "HH:mm", out var ends)) return BadRequest(new { error = "Invalid ends." });

        var exceptionId = Guid.NewGuid();
        var command = new CreateRosterExceptionCommand
        {
            RosterExceptionId = exceptionId, StaffAccountId = staffAccountId, BranchId = branchId, Date = request.Date,
            Kind = request.Kind, Starts = starts, Ends = ends, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { rosterExceptionId = exceptionId }) : BadRequest(response.ValidationResult);
    }

    [HttpDelete("roster-exceptions/{id:guid}")]
    [RequireAction(Actions.Rosters.Manage)]
    public async Task<IActionResult> CancelException(Guid branchId, Guid id)
    {
        var command = new CancelRosterExceptionCommand
        {
            BranchId = branchId, RosterExceptionId = id,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("availability")]
    [RequireAction(Actions.Rosters.View)]
    public async Task<IActionResult> GetAvailability(Guid branchId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] Guid? staffAccountId, [FromQuery] bool restrictToOpeningHours = false)
    {
        if (to.DayNumber - from.DayNumber > LeaveRules.MaxAvailabilityRangeDays)
            return BadRequest(new { error = $"The range cannot exceed {LeaveRules.MaxAvailabilityRangeDays} days." });

        var response = await dispatcher.GetResultAsync(new GetStaffAvailabilityQuery
        {
            BranchId = branchId, From = from, To = to, StaffAccountId = staffAccountId, RestrictToOpeningHours = restrictToOpeningHours,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }
}
