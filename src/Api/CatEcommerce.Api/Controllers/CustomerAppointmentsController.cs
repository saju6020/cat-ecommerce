using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Clinic;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4 -- every handler resolves the appointment by id and caller; someone
/// else's appointment is 404 (§12.3).</summary>
[ApiController]
[Route("api/customers/me")]
[RequireCustomerSession]
public sealed class CustomerAppointmentsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost("appointment-holds")]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Booking)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> PlaceHold([FromBody] PlaceSlotHoldRequest request)
    {
        var appointmentId = Guid.NewGuid();
        var command = new PlaceSlotHoldCommand
        {
            AppointmentId = appointmentId, BranchId = request.BranchId, ServiceId = request.ServiceId, CatId = request.CatId,
            StartsOn = request.StartsOn, PerformerId = request.PerformerId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (AppointmentHoldResult)response.Result;
        return StatusCode(201, new { appointmentId = result.AppointmentId, startsOn = result.StartsOn, endsOn = result.EndsOn, performerId = result.PerformerId, roomId = result.RoomId, holdExpiresOn = result.HoldExpiresOn });
    }

    [HttpDelete("appointment-holds/{appointmentId:guid}")]
    public async Task<IActionResult> ReleaseHold(Guid appointmentId)
    {
        var response = await dispatcher.SendAsync(new ReleaseSlotHoldCommand
        { AppointmentId = appointmentId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("appointments/{appointmentId:guid}/confirm")]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Change)]
    public async Task<IActionResult> Confirm(Guid appointmentId, [FromBody] ConfirmAppointmentRequest request)
    {
        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointmentId, ReasonForVisit = request.ReasonForVisit,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return await GetDetailAsync(appointmentId);
        return RespondToFailure(response.ValidationResult, notFoundCode: BusinessViolationCodes.HoldNotFound);
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments([FromQuery] string scope, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var query = new GetCustomerAppointmentsQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId, Scope = string.IsNullOrWhiteSpace(scope) ? "upcoming" : scope,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = pageSize.Value;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("appointments/{appointmentId:guid}")]
    public Task<IActionResult> GetDetail(Guid appointmentId) => GetDetailAsync(appointmentId);

    [HttpPost("appointments/{appointmentId:guid}/reschedule")]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Change)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> Reschedule(Guid appointmentId, [FromBody] RescheduleAppointmentRequest request)
    {
        var command = new RescheduleAppointmentCommand
        {
            AppointmentId = appointmentId, StartsOn = request.StartsOn, PerformerId = request.PerformerId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return await GetDetailAsync(appointmentId);
        return RespondToFailure(response.ValidationResult, notFoundCode: BusinessViolationCodes.AppointmentNotFound);
    }

    [HttpPost("appointments/{appointmentId:guid}/cancel")]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Change)]
    public async Task<IActionResult> Cancel(Guid appointmentId, [FromBody] CancelAppointmentRequest request)
    {
        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointmentId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return NoContent();
        return RespondToFailure(response.ValidationResult, notFoundCode: BusinessViolationCodes.AppointmentNotFound);
    }

    private async Task<IActionResult> GetDetailAsync(Guid appointmentId)
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerAppointmentDetailQuery
        { AppointmentId = appointmentId, CustomerId = userContextProvider.GetUserContext().UserId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>A customer never sees another customer's appointment/hold id, and never sees why
    /// beyond "not found" (§12.3).</summary>
    private IActionResult RespondToFailure(Platform.Infrastructure.Core.Validation.ValidationResponse? validationResult, string? notFoundCode = null)
    {
        var codes = validationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        if ((notFoundCode is not null && codes.Contains(notFoundCode)) || codes.Contains(BusinessViolationCodes.CatNotFound))
            return NotFound();

        var isConflict = codes.Contains(BusinessViolationCodes.SlotNoLongerAvailable);
        return isConflict ? Conflict(validationResult) : BadRequest(validationResult);
    }
}
