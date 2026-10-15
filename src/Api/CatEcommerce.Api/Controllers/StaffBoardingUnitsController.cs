using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 -- units, maintenance, rate cards and the care routine. Staff; the branch comes from <c>X-Branch-Id</c>. Reads need
/// <c>Boarding.ViewUnits</c>, writes <c>Boarding.ManageUnits</c> (Administrator).</summary>
[ApiController]
[Route("api/staff/boarding")]
public sealed class StaffBoardingUnitsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpGet("units")]
    [RequireAction(Actions.Boarding.ViewUnits)]
    public async Task<IActionResult> GetUnits()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingUnitsQuery { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("units")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> CreateUnit([FromBody] CreateBoardingUnitRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var unitId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new CreateBoardingUnitCommand
        {
            UnitId = unitId, BranchId = branchId, Code = request.Code, Name = request.Name, Type = request.UnitType, CapacityCats = request.CapacityCats,
            Block = request.Block, SortOrder = request.SortOrder, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { unitId }));
    }

    [HttpPut("units/{unitId:guid}")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> UpdateUnit(Guid unitId, [FromBody] UpdateBoardingUnitRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new UpdateBoardingUnitCommand
        {
            UnitId = unitId, BranchId = branchId, Name = request.Name, Type = request.UnitType, CapacityCats = request.CapacityCats,
            Block = request.Block, SortOrder = request.SortOrder, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, NoContent);
    }

    [HttpPut("units/{unitId:guid}/activation")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> SetActivation(Guid unitId, [FromBody] SetBoardingUnitActivationRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new SetBoardingUnitActivationCommand
        { UnitId = unitId, BranchId = branchId, IsActive = request.IsActive, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("units/{unitId:guid}/maintenance")]
    [RequireAction(Actions.Boarding.ViewUnits)]
    public async Task<IActionResult> GetMaintenance(Guid unitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetUnitMaintenanceQuery { UnitId = unitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("units/{unitId:guid}/maintenance")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> CreateMaintenance(Guid unitId, [FromBody] CreateMaintenanceBlockRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var blockId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new CreateMaintenanceBlockCommand
        {
            BlockId = blockId, UnitId = unitId, BranchId = branchId, StartsOn = request.StartsOn, EndsOn = request.EndsOn, Reason = request.Reason,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { blockId }));
    }

    [HttpDelete("units/{unitId:guid}/maintenance/{blockId:guid}")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> CancelMaintenance(Guid unitId, Guid blockId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CancelMaintenanceBlockCommand
        { BlockId = blockId, UnitId = unitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("rate-cards")]
    [RequireAction(Actions.Boarding.ViewUnits)]
    public async Task<IActionResult> GetRateCards([FromQuery] string? unitType)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingRateCardsQuery { BranchId = branchId, UnitType = unitType, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("rate-cards")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> SetRateCard([FromBody] SetBoardingRateCardRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        if (!TryParseMoney(request.NightlyRate, out var nightly) || !TryParseMoney(request.WeeklyRate, out var weekly) || !TryParseMoney(request.MonthlyRate, out var monthly))
            return BadRequest(new { error = "Invalid amount." });

        var additional = 0m;
        if (request.AdditionalCatNightlyRate is not null && !TryParseMoney(request.AdditionalCatNightlyRate, out additional))
            return BadRequest(new { error = "Invalid amount." });

        var rateCardId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new SetBoardingRateCardCommand
        {
            RateCardId = rateCardId, BranchId = branchId, UnitType = request.UnitType, NightlyRate = nightly, WeeklyRate = weekly, MonthlyRate = monthly, AdditionalCatNightlyRate = additional,
            TaxClassId = request.TaxClassId, EffectiveFrom = request.EffectiveFrom, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { rateCardId }));
    }

    [HttpGet("care-routine")]
    [RequireAction(Actions.Boarding.ViewUnits)]
    public async Task<IActionResult> GetCareRoutine()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingCareRoutineQuery { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPut("care-routine")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> SetCareRoutine([FromBody] SetBoardingCareRoutineRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var times = new List<RoutineTimeItem>();
        foreach (var item in request.Times)
        {
            if (!TryParseTime(item.Time, out var time)) return BadRequest(new { error = $"'{item.Time}' is not a time (HH:mm)." });
            times.Add(new RoutineTimeItem(item.Kind, time));
        }

        var response = await Dispatcher.SendAsync(new SetBoardingCareRoutineCommand
        { BranchId = branchId, Times = times, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }
}
