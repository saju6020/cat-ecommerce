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

/// <summary>HO-BRD-001 section 12.4 (Phase E) -- check-in, the day task list, observations and isolation. Staff; the branch is the active branch.</summary>
[ApiController]
[Route("api/staff/boarding")]
public sealed class StaffBoardingCareController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpPost("stays/{stayId:guid}/check-in")]
    [RequireAction(Actions.Boarding.CheckInOut)]
    public async Task<IActionResult> CheckIn(Guid stayId, [FromBody] CheckInRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var cats = request.Cats.Select(c => new CheckInCatInput(
            c.CatId, c.IdentityConfirmed, c.VaccinationProofVerified, c.WeightKg, c.ArrivalHealthObservation,
            c.Belongings.Select(b => new BelongingItemInput(b.Kind, b.Description, b.FileIds)).ToList(), c.NoBelongings,
            c.Signature is null ? null : new SignatureInput(c.Signature.SignerName, c.Signature.Method, c.Signature.Strokes?.GetRawText(), c.Signature.ConfirmedStatement))).ToList();

        var response = await Dispatcher.SendAsync(new CheckInBoardingStayCommand
        { StayId = stayId, BranchId = branchId, Cats = cats, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(new { stay = detail.Result });
    }

    [HttpGet("tasks")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetTasks([FromQuery] DateOnly date, [FromQuery] Guid? unitId, [FromQuery] string? assignedTo, [FromQuery] string? status)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetCareTaskDayListQuery
        {
            BranchId = branchId, Date = date, UnitId = unitId, AssignedTo = string.Equals(assignedTo, "me", StringComparison.OrdinalIgnoreCase) ? Caller.UserId : null,
            Status = status, UserContext = Caller, CorrelationId = Guid.NewGuid()
        });
        return Ok(response.Result);
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    [RequireAction(Actions.Boarding.RecordCare)]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromBody] CompleteCareTaskRequest? request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        // No required body: the tablet's second tap is the whole request.
        var response = await Dispatcher.SendAsync(new CompleteCareTaskCommand
        { TaskId = taskId, BranchId = branchId, Note = request?.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("tasks/{taskId:guid}/skip")]
    [RequireAction(Actions.Boarding.RecordCare)]
    public async Task<IActionResult> SkipTask(Guid taskId, [FromBody] SkipCareTaskRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new SkipCareTaskCommand
        { TaskId = taskId, BranchId = branchId, Reason = request.Reason, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/observations")]
    [RequireAction(Actions.Boarding.RecordCare)]
    public async Task<IActionResult> RecordObservation(Guid stayId, [FromBody] RecordObservationRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var observationId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new RecordCareObservationCommand
        {
            ObservationId = observationId, StayId = stayId, CatId = request.CatId, BranchId = branchId, PortionEaten = request.PortionEaten, WaterIntake = request.WaterIntake,
            LitterOutput = request.LitterOutput, Mood = request.Mood, Notes = request.Notes, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { observationId }));
    }

    [HttpGet("stays/{stayId:guid}/observations")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetObservations(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetStayObservationsQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/cats/{catId:guid}/suspected-infectious")]
    [RequireAction(Actions.Boarding.Isolation)]
    [BoardingClaimEndpoint]
    public async Task<IActionResult> Flag(Guid stayId, Guid catId, [FromBody] FlagInfectiousRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new FlagSuspectedInfectiousCommand
        { StayId = stayId, CatId = catId, BranchId = branchId, Note = request.Note, TargetIsolationUnitId = request.TargetIsolationUnitId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpDelete("stays/{stayId:guid}/cats/{catId:guid}/suspected-infectious")]
    [RequireAction(Actions.Boarding.ClearIsolation)]
    public async Task<IActionResult> Clear(Guid stayId, Guid catId, [FromBody] ClearInfectiousRequest? request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ClearSuspectedInfectiousCommand
        { StayId = stayId, CatId = catId, BranchId = branchId, Note = request?.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/isolation-entries")]
    [RequireAction(Actions.Boarding.Isolation)]
    public async Task<IActionResult> EnterIsolation(Guid stayId, [FromBody] IsolationEntryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var entryId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new RecordIsolationEntryCommand
        { EntryId = entryId, StayId = stayId, BranchId = branchId, Purpose = request.Purpose, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, () => StatusCode(201, new { entryId }));
    }

    [HttpPost("stays/{stayId:guid}/isolation-entries/{entryId:guid}/exit")]
    [RequireAction(Actions.Boarding.Isolation)]
    public async Task<IActionResult> ExitIsolation(Guid stayId, Guid entryId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordIsolationExitCommand
        { EntryId = entryId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }
}
