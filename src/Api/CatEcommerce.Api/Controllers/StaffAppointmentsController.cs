using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Clinic;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4 -- staff. Branch from <c>X-Branch-Id</c> (HO-ADM-001 §8.3).</summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffAppointmentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet("appointments/slots")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetSlots([FromQuery] Guid serviceId, [FromQuery] DateOnly date, [FromQuery] Guid? performerId, [FromQuery] Guid? catId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStaffAvailableSlotsQuery
        {
            BranchId = branchId, ServiceId = serviceId, Date = date, PerformerId = performerId, CatId = catId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("appointments/calendar")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetCalendar([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string view, [FromQuery] string? ids)
    {
        if (to.DayNumber - from.DayNumber > AppointmentRules.MaxCalendarRangeDays)
            return BadRequest(new { error = $"The range cannot exceed {AppointmentRules.MaxCalendarRangeDays} days." });
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        List<Guid>? idList = null;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            idList = [];
            foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Guid.TryParse(part, out var id)) return BadRequest(new { error = $"'{part}' is not a valid id." });
                idList.Add(id);
            }
        }

        var response = await dispatcher.GetResultAsync(new GetCalendarQuery
        { BranchId = branchId, From = from, To = to, View = view, Ids = idList, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpGet("appointments/day")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetDay([FromQuery] DateOnly date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDayListQuery { BranchId = branchId, Date = date, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("appointments/flagged")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetFlagged()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetFlaggedAppointmentsQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("appointments/{id:guid}")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetAppointmentDetailQuery { AppointmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("appointments")]
    [RequireAction(Actions.Appointments.Book)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var appointmentId = Guid.NewGuid();
        var command = new BookAppointmentByStaffCommand
        {
            AppointmentId = appointmentId, BranchId = branchId, CustomerId = request.CustomerId, CatId = request.CatId,
            ServiceId = request.ServiceId, StartsOn = request.StartsOn, PerformerId = request.PerformerId, RoomId = request.RoomId,
            ReasonForVisit = request.ReasonForVisit, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { appointmentId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/move")]
    [RequireAction(Actions.Appointments.Book)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveAppointmentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var command = new MoveAppointmentCommand
        {
            AppointmentId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, StartsOn = request.StartsOn,
            PerformerId = request.PerformerId, RoomId = request.RoomId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess)
        {
            var detail = await dispatcher.GetResultAsync(new GetAppointmentDetailQuery { AppointmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
            return Ok(detail.Result);
        }

        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        if (codes.Contains(BusinessViolationCodes.AppointmentNotFound)) return NotFound();
        if (codes.Contains(BusinessViolationCodes.StaleCalendarVersion)) return Conflict(response.ValidationResult);
        return BadRequest(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/cancel")]
    [RequireAction(Actions.Appointments.Book)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelAppointmentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        // Backlog #33: the resolved caller kind decides staff-vs-customer, not a user-name string.
        var isStaff = HttpContext.Items[ResolvedCaller.HttpContextItemsKey] is ResolvedCaller { Kind: ResolvedCallerKind.Staff };
        var command = new CancelAppointmentCommand
        {
            AppointmentId = id, Reason = request.Reason, IsStaff = isStaff, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/check-in")]
    [RequireAction(Actions.Appointments.CheckIn)]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var command = new CheckInAppointmentCommand
        { AppointmentId = id, BranchId = branchId, TriagePriority = request.TriagePriority, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpPut("appointments/{id:guid}/triage")]
    [RequireAction(Actions.Appointments.CheckIn)]
    public async Task<IActionResult> Triage(Guid id, [FromBody] SetTriagePriorityRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var command = new SetTriagePriorityCommand
        { AppointmentId = id, BranchId = branchId, TriagePriority = request.TriagePriority, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/start")]
    [RequireAction(Actions.Appointments.Treat)]
    public async Task<IActionResult> Start(Guid id, [FromBody] StartAppointmentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var command = new StartAppointmentCommand
        { AppointmentId = id, BranchId = branchId, PerformerId = request.PerformerId, RoomId = request.RoomId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/complete")]
    [RequireAction(Actions.Appointments.Treat)]
    public async Task<IActionResult> Complete(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CompleteAppointmentCommand
        { AppointmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpPost("appointments/{id:guid}/no-show")]
    [RequireAction(Actions.Appointments.RecordNoShow)]
    public async Task<IActionResult> RecordNoShow(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordNoShowCommand
        { AppointmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }

    [HttpGet("customers/{customerId:guid}/appointment-summary")]
    [RequireAction(Actions.Appointments.View)]
    public async Task<IActionResult> GetCustomerSummary(Guid customerId)
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerAppointmentSummaryQuery { CustomerId = customerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    /// <summary>By-id actions: an appointment that does not exist or belongs to another branch is the
    /// same <c>404</c> (backlog #8) -- never a reason that would confirm another branch's id.</summary>
    private IActionResult RespondToFailure(Platform.Infrastructure.Core.Validation.ValidationResponse? validationResult)
    {
        var codes = validationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        return codes.Contains(BusinessViolationCodes.AppointmentNotFound) ? NotFound() : BadRequest(validationResult);
    }

    /// <summary>ADR-046 -- a refused <c>X-Branch-Id</c> (or no assignment/default) is a <c>403</c>
    /// naming the refusal, never silently downgraded.</summary>
    private bool TryResolveBranch(out Guid branchId, out IActionResult? refusal)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is { } id)
        {
            branchId = id;
            refusal = null;
            return true;
        }

        branchId = default;
        refusal = StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });
        return false;
    }
}
