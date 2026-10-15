using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Prescription;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.CommandHandlers.Prescription;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-PHM-001 §12.4 -- prescribing (US-PHM-01, 02, 03). Writing a prescription is <c>Prescriptions.Issue</c>: Vet alone (BR-6, US-CLN-22, AC-18) -- the filter
/// refuses anyone else with a <c>403</c> and an <c>AuthorizationDenied</c> audit row before an action runs. A violation that needs the screen's reaction (no
/// recent weight, warnings not acknowledged, warnings changed) carries the fresh <c>assessment</c> in its body. The safety check is a query and writes nothing.
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffPrescriptionsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpPost("cats/{catId:guid}/prescriptions/safety-check")]
    [RequireAction(Actions.Prescriptions.Issue)]
    public async Task<IActionResult> CheckSafety(Guid catId, [FromBody] SafetyCheckRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new CheckPrescriptionSafetyQuery
        {
            CatId = catId, MedicineId = request.MedicineId, DoseAmount = request.Dose.Amount, DoseUnit = request.Dose.Unit, Route = request.Route,
            Frequency = request.Frequency, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("cats/{catId:guid}/prescriptions")]
    [RequireAction(Actions.Prescriptions.Issue)]
    public async Task<IActionResult> Issue(Guid catId, [FromBody] IssuePrescriptionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var prescriptionId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new IssuePrescriptionCommand
        {
            PrescriptionId = prescriptionId, CatId = catId, VisitId = request.VisitId, MedicineId = request.MedicineId, BranchId = branchId,
            Dose = new DoseModel(request.Dose.Amount, request.Dose.Unit), Route = request.Route, Frequency = request.Frequency, DurationDays = request.DurationDays,
            Quantity = request.Quantity, Instructions = request.Instructions, StartsOn = request.StartsOn, IsRepeatable = request.IsRepeatable,
            RepeatsAuthorised = request.RepeatsAuthorised, ValidUntil = request.ValidUntil, AcknowledgedWarningsFingerprint = request.AcknowledgedWarningsFingerprint,
            AllergyAcknowledgementReason = request.AllergyAcknowledgementReason, DoseAcknowledgementNote = request.DoseAcknowledgementNote,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess)
        {
            var issued = (IssuedPrescription)response.Result!;
            return StatusCode(201, new { prescriptionId = issued.PrescriptionId, prescriptionNumber = issued.PrescriptionNumber });
        }

        return RespondToFailure(response, WithAssessment(response), BusinessViolationCodes.CatNotFound);
    }

    [HttpGet("cats/{catId:guid}/prescriptions")]
    [RequireAction(Actions.Prescriptions.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.ClinicalHistory))]
    public async Task<IActionResult> GetCatPrescriptions(
        Guid catId, [FromQuery] string? scope, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (scope is not null && scope is not ("active" or "all")) return BadRequest(new { error = "scope must be 'active' or 'all'." });

        var query = new GetCatPrescriptionsQuery
        {
            CatId = catId, Scope = scope ?? "active", BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } paging) return paging;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("prescriptions/{prescriptionId:guid}")]
    [RequireAction(Actions.Prescriptions.View)]
    public async Task<IActionResult> GetPrescription(Guid prescriptionId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPrescriptionDetailQuery
        {
            PrescriptionId = prescriptionId, CallerRoles = CallerRoles(), BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        if (response.Result is not { } detail) return NotFound();

        // NFR-SEC-8: the cat is only known after the read, so the access row is written here, through HO-EMR-001's command, and fails closed.
        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", detail.CatId, detail.Id))
            return StatusCode(500, new { error = "The request could not be completed." });

        Response.Headers.CacheControl = "no-store";
        return Ok(detail);
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/cancel")]
    [RequireAction(Actions.Prescriptions.Issue)]
    public async Task<IActionResult> Cancel(Guid prescriptionId, [FromBody] CancelPrescriptionRequest request)
    {
        var response = await dispatcher.SendAsync(new CancelPrescriptionCommand
        {
            PrescriptionId = prescriptionId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.PrescriptionNotFound);
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/renew")]
    [RequireAction(Actions.Prescriptions.Issue)]
    public async Task<IActionResult> Renew(Guid prescriptionId, [FromBody] RenewPrescriptionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var newId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RenewPrescriptionCommand
        {
            NewPrescriptionId = newId, FromPrescriptionId = prescriptionId, VisitId = request.VisitId, BranchId = branchId,
            Dose = new DoseModel(request.Dose.Amount, request.Dose.Unit), Route = request.Route, Frequency = request.Frequency, DurationDays = request.DurationDays,
            Quantity = request.Quantity, Instructions = request.Instructions, StartsOn = request.StartsOn, IsRepeatable = request.IsRepeatable,
            RepeatsAuthorised = request.RepeatsAuthorised, ValidUntil = request.ValidUntil, AcknowledgedWarningsFingerprint = request.AcknowledgedWarningsFingerprint,
            AllergyAcknowledgementReason = request.AllergyAcknowledgementReason, DoseAcknowledgementNote = request.DoseAcknowledgementNote,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess)
        {
            var issued = (IssuedPrescription)response.Result!;
            return StatusCode(201, new { prescriptionId = issued.PrescriptionId, prescriptionNumber = issued.PrescriptionNumber });
        }

        return RespondToFailure(response, WithAssessment(response), BusinessViolationCodes.PrescriptionNotFound);
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/repeat-requests/{requestId:guid}/decline")]
    [RequireAction(Actions.Prescriptions.Issue)]
    public async Task<IActionResult> DeclineRepeat(Guid prescriptionId, Guid requestId, [FromBody] DeclineRepeatRequestRequest request)
    {
        var response = await dispatcher.SendAsync(new DeclineRepeatRequestCommand
        {
            PrescriptionId = prescriptionId, RepeatRequestId = requestId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.PrescriptionNotFound, BusinessViolationCodes.RepeatRequestNotFound);
    }

    [HttpGet("visits/{visitId:guid}/prescriptions")]
    [RequireAction(Actions.Prescriptions.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.Visit))]
    public async Task<IActionResult> GetVisitPrescriptions(Guid visitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var prescriptions = await dispatcher.GetResultAsync(new GetVisitPrescriptionsQuery
        { VisitId = visitId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        var diets = await dispatcher.GetResultAsync(new GetVisitDietAuthorisationsQuery
        { VisitId = visitId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(new { prescriptions = prescriptions.Result, dietAuthorisations = diets.Result });
    }

    /// <summary>The fresh assessment, for the three failures whose answer the screen must react to.</summary>
    private static IReadOnlyDictionary<string, object?>? WithAssessment(Platform.Infrastructure.Core.Commands.CommandResponse response) =>
        response.Result is SafetyAssessment assessment
            ? new Dictionary<string, object?> { ["assessment"] = SafetyAssessmentViewModel.From(assessment) }
            : null;
}
