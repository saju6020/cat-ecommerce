using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Surgery;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Surgery;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-SRG-001 §12.4 (US-SRG-04, US-SRG-08, ADR-162) -- the anaesthesia log. <b>There is no PUT, PATCH or DELETE on any log route</b> (I-D5 asserts 405): an entry is created once, and a correction
/// is a NEW entry. Drugs are <c>Surgery.LogDrug</c> (Vet alone); vitals, milestones and their corrections are <c>Surgery.LogVitals</c> (Nurse, Vet) -- a drug correction is re-checked for a vet
/// in the handler. The endpoints that carry a witness credential share HO-PHM-001's per-staff witness rate limit (guessing a colleague's password at the table is throttled before the account lockout).
/// Reading the log is a clinical-record access and is logged (NFR-SEC-8).
/// </summary>
[ApiController]
[Route("api/staff/surgeries/{id:guid}/anaesthesia-log")]
public sealed class StaffSurgeryAnaesthesiaController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : SurgeryControllerBase(activeBranchAccessor)
{
    private UserContext User => userContextProvider.GetUserContext();

    [HttpGet]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetAnaesthesiaLogQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        if (response.Result is null) return NotFound();

        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", response.Result.CatId, id)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpPost("drugs")]
    [RequireAction(Actions.Surgery.LogDrug)]
    [EnableRateLimiting(PharmacyRateLimitPolicies.Witness)]
    public async Task<IActionResult> LogDrug(Guid id, [FromBody] LogAnaesthesiaDrugRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new LogAnaesthesiaDrugCommand
        {
            EntryId = request.EntryId is { } key && key != Guid.Empty ? key : Guid.NewGuid(), SurgeryId = id, BranchId = branchId, MedicineId = request.MedicineId, DoseAmount = request.DoseAmount, DoseUnit = request.DoseUnit,
            Route = request.Route, OccurredOn = request.OccurredOn, AdministeredById = request.AdministeredById, BatchId = request.BatchId, QuantityDrawn = request.QuantityDrawn,
            QuantityWasted = request.QuantityWasted, WitnessIdentifier = request.WitnessIdentifier, WitnessPassword = request.WitnessPassword,
            FefoOverrideReason = request.FefoOverrideReason, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return RespondToFailure(response, RouteNotFoundCodes);
        var result = (LogDrugResult)response.Result!;
        return StatusCode(201, new { entryId = result.EntryId, registerEntryId = result.RegisterEntryId, stockShortfall = result.StockShortfall });
    }

    [HttpPost("vitals")]
    [RequireAction(Actions.Surgery.LogVitals)]
    public async Task<IActionResult> LogVitals(Guid id, [FromBody] LogAnaesthesiaVitalsRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var entryId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new LogAnaesthesiaVitalsCommand
        {
            EntryId = entryId, SurgeryId = id, BranchId = branchId, OccurredOn = request.OccurredOn, HeartRateBpm = request.HeartRateBpm, RespiratoryRatePerMin = request.RespiratoryRatePerMin,
            SpO2Percent = request.SpO2Percent, EtCo2MmHg = request.EtCo2MmHg, TemperatureCelsius = request.TemperatureCelsius, SystolicMmHg = request.SystolicMmHg,
            MeanArterialMmHg = request.MeanArterialMmHg, Note = request.Note, ConfirmImplausible = request.ConfirmImplausible, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { entryId }) : RespondToFailure(response, RouteNotFoundCodes);
    }

    [HttpPost("milestones")]
    [RequireAction(Actions.Surgery.LogVitals)]
    public async Task<IActionResult> LogMilestone(Guid id, [FromBody] LogAnaesthesiaMilestoneRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var entryId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new LogAnaesthesiaMilestoneCommand
        {
            EntryId = entryId, SurgeryId = id, BranchId = branchId, Milestone = request.Milestone, OccurredOn = request.OccurredOn, Note = request.Note,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { entryId }) : RespondToFailure(response, RouteNotFoundCodes);
    }

    [HttpPost("{entryId:guid}/corrections")]
    [RequireAction(Actions.Surgery.LogVitals)]
    [EnableRateLimiting(PharmacyRateLimitPolicies.Witness)]
    public async Task<IActionResult> Correct(Guid id, Guid entryId, [FromBody] CorrectAnaesthesiaEntryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var correctionId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CorrectAnaesthesiaEntryCommand
        {
            EntryId = correctionId, SurgeryId = id, BranchId = branchId, CorrectsEntryId = entryId, Reason = request.Reason, OccurredOn = request.OccurredOn,
            Drug = request.Drug is null ? null : new AnaesthesiaDrugCorrectionInput(request.Drug.DoseAmount, request.Drug.DoseUnit, request.Drug.Route, request.Drug.AdministeredById, request.Drug.QuantityDrawn, request.Drug.QuantityWasted),
            Vitals = request.Vitals is null ? null : new AnaesthesiaVitalsCorrectionInput(request.Vitals.HeartRateBpm, request.Vitals.RespiratoryRatePerMin, request.Vitals.SpO2Percent,
                request.Vitals.EtCo2MmHg, request.Vitals.TemperatureCelsius, request.Vitals.SystolicMmHg, request.Vitals.MeanArterialMmHg, request.Vitals.Note, request.Vitals.ConfirmImplausible),
            Milestone = request.Milestone is null ? null : new AnaesthesiaMilestoneCorrectionInput(request.Milestone.Milestone, request.Milestone.Note),
            WitnessIdentifier = request.WitnessIdentifier, WitnessPassword = request.WitnessPassword, FefoOverrideReason = request.FefoOverrideReason,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { entryId = correctionId }) : RespondToFailure(response, RouteNotFoundCodes);
    }
}
