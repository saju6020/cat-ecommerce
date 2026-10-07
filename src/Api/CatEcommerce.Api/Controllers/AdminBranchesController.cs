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

/// <summary>HO-ADM-001 §12.4 -- branches, hours, closures and rooms.</summary>
[ApiController]
[Route("api/admin/branches")]
public sealed class AdminBranchesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetBranches()
    {
        var response = await dispatcher.GetResultAsync(new GetAdminBranchesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{branchId:guid}")]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetBranch(Guid branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetAdminBranchQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequest request)
    {
        var branchId = Guid.NewGuid();
        var command = new CreateBranchCommand
        {
            BranchId = branchId, Code = request.Code, Name = request.Name, TimeZoneId = request.TimeZoneId,
            AddressLine = request.AddressLine, PhoneNumber = request.PhoneNumber, Email = request.Email,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { branchId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{branchId:guid}")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> UpdateBranchDetails(Guid branchId, [FromBody] UpdateBranchDetailsRequest request)
    {
        var command = new UpdateBranchDetailsCommand
        {
            BranchId = branchId, Name = request.Name, TimeZoneId = request.TimeZoneId,
            AddressLine = request.AddressLine, PhoneNumber = request.PhoneNumber, Email = request.Email,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPut("{branchId:guid}/hours")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> SetWeeklyHours(Guid branchId, [FromBody] SetBranchWeeklyHoursRequest request)
    {
        var intervals = new List<OpeningIntervalInput>();
        foreach (var i in request.Intervals)
        {
            if (!Enum.TryParse<DayOfWeek>(i.DayOfWeek, true, out var day)) return BadRequest(new { error = $"'{i.DayOfWeek}' is not a valid day of week." });
            if (!TimeOnly.TryParseExact(i.Opens, "HH:mm", out var opens)) return BadRequest(new { error = $"'{i.Opens}' is not a valid time." });
            if (!TimeOnly.TryParseExact(i.Closes, "HH:mm", out var closes)) return BadRequest(new { error = $"'{i.Closes}' is not a valid time." });
            intervals.Add(new OpeningIntervalInput(day, opens, closes));
        }

        var command = new SetBranchWeeklyHoursCommand
        {
            BranchId = branchId, Intervals = intervals,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPut("{branchId:guid}/activation")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> SetActivation(Guid branchId, [FromBody] SetBranchActivationRequest request)
    {
        var command = new SetBranchActivationCommand
        {
            BranchId = branchId, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("{branchId:guid}/closures")]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetClosures(Guid branchId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var response = await dispatcher.GetResultAsync(new GetBranchClosuresQuery
        { BranchId = branchId, From = from, To = to, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        TruncationHeader.ApplyIfAtCap(Response, response.Result.Count);
        return Ok(response.Result);
    }

    [HttpPost("{branchId:guid}/closures")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> CreateClosure(Guid branchId, [FromBody] CreateBranchClosureRequest request)
    {
        // Gate fix 2026-09-26: parse exactly once each -- the previous shape called
        // TryParseExact to validate, then ParseExact again (on the same, already-known-good
        // string) to actually get the value.
        TimeOnly? partialFrom = null, partialTo = null;
        if (request.PartialFrom is not null)
        {
            if (!TimeOnly.TryParseExact(request.PartialFrom, "HH:mm", out var pf)) return BadRequest(new { error = "Invalid partialFrom." });
            partialFrom = pf;
        }
        if (request.PartialTo is not null)
        {
            if (!TimeOnly.TryParseExact(request.PartialTo, "HH:mm", out var pt)) return BadRequest(new { error = "Invalid partialTo." });
            partialTo = pt;
        }

        var closureId = Guid.NewGuid();
        var command = new CreateBranchClosureCommand
        {
            ClosureId = closureId, BranchId = branchId, StartsOn = request.StartsOn, EndsOn = request.EndsOn,
            Kind = request.Kind, Reason = request.Reason, PartialFrom = partialFrom, PartialTo = partialTo,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { closureId }) : BadRequest(response.ValidationResult);
    }

    [HttpDelete("{branchId:guid}/closures/{closureId:guid}")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> CancelClosure(Guid branchId, Guid closureId)
    {
        var command = new CancelBranchClosureCommand
        {
            BranchId = branchId, ClosureId = closureId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("{branchId:guid}/rooms")]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetRooms(Guid branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetRoomsQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("{branchId:guid}/rooms")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> CreateRoom(Guid branchId, [FromBody] CreateRoomRequest request)
    {
        var roomId = Guid.NewGuid();
        var command = new CreateRoomCommand
        {
            RoomId = roomId, BranchId = branchId, Name = request.Name, Kind = request.Kind,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { roomId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{branchId:guid}/rooms/{roomId:guid}")]
    [RequireAction(Actions.Branches.Manage)]
    public async Task<IActionResult> UpdateRoom(Guid branchId, Guid roomId, [FromBody] UpdateRoomRequest request)
    {
        var command = new UpdateRoomCommand
        {
            RoomId = roomId, BranchId = branchId, Name = request.Name, Kind = request.Kind, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("{branchId:guid}/calendar")]
    [RequireAction(Actions.Branches.View)]
    public async Task<IActionResult> GetCalendar(Guid branchId, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to.DayNumber - from.DayNumber > BranchRules.MaxCalendarRangeDays)
            return BadRequest(new { error = $"The range cannot exceed {BranchRules.MaxCalendarRangeDays} days." });

        var response = await dispatcher.GetResultAsync(new GetBranchCalendarQuery { BranchId = branchId, From = from, To = to, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
