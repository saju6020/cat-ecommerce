using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 (Phase F) -- stay media, incidents and the dashboard. Staff; the branch is the active branch.</summary>
[ApiController]
[Route("api/staff/boarding")]
public sealed class StaffBoardingIncidentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpPost("stays/{stayId:guid}/media")]
    [RequireAction(Actions.Boarding.RecordCare)]
    public async Task<IActionResult> AddMedia(Guid stayId, [FromBody] AddStayMediaRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var mediaId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new AddStayMediaCommand
        {
            MediaId = mediaId, StayId = stayId, BranchId = branchId, CatId = request.CatId, IncidentId = request.IncidentId, FileId = request.FileId, Caption = request.Caption,
            CapturedOnLocalDate = request.CapturedOnLocalDate, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { mediaId }));
    }

    [HttpGet("stays/{stayId:guid}/media")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetMedia(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetStayMediaQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/media/{mediaId:guid}/hide")]
    [RequireAction(Actions.Boarding.RecordCare)]
    public async Task<IActionResult> HideMedia(Guid stayId, Guid mediaId, [FromBody] HideStayMediaRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new HideStayMediaCommand
        { MediaId = mediaId, StayId = stayId, BranchId = branchId, Reason = request.Reason, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/incidents")]
    [RequireAction(Actions.Boarding.RecordIncident)]
    public async Task<IActionResult> RecordIncident(Guid stayId, [FromBody] RecordIncidentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var incidentId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new RecordIncidentCommand
        {
            IncidentId = incidentId, StayId = stayId, CatId = request.CatId, BranchId = branchId, Kind = request.Kind, Severity = request.Severity, Description = request.Description,
            ImmediateAction = request.ImmediateAction, OccurredOn = request.OccurredOn, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { incidentId }));
    }

    [HttpGet("incidents")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetIncidents([FromQuery] string? status, [FromQuery] Guid? stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetIncidentsQuery { BranchId = branchId, Status = status, StayId = stayId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("incidents/{incidentId:guid}/acknowledgement")]
    [RequireAction(Actions.Boarding.FollowUpIncident)]
    public async Task<IActionResult> Acknowledge(Guid incidentId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordIncidentAcknowledgementCommand
        { IncidentId = incidentId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("incidents/{incidentId:guid}/phone-call")]
    [RequireAction(Actions.Boarding.FollowUpIncident)]
    public async Task<IActionResult> PhoneCall(Guid incidentId, [FromBody] RecordIncidentPhoneCallRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordIncidentPhoneCallCommand
        { IncidentId = incidentId, BranchId = branchId, Outcome = request.Outcome, OwnerReached = request.OwnerReached, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("incidents/{incidentId:guid}/resolve")]
    [RequireAction(Actions.Boarding.FollowUpIncident)]
    public async Task<IActionResult> Resolve(Guid incidentId, [FromBody] ResolveIncidentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ResolveIncidentCommand
        { IncidentId = incidentId, BranchId = branchId, Resolution = request.Resolution, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("dashboard")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> Dashboard()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingDashboardQuery { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
