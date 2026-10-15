using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Surgery;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.CommandHandlers.Surgery;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-SRG-001 §12.4 (US-SRG-01, 02, 03) -- scheduling, the checklist and the start gate. Every action is scoped to the caller's ACTIVE BRANCH (a surgery of another branch is
/// <c>404</c>); the branch comes from the header, never a body. A booking answers <c>201</c> with the real id, or <c>400</c> with ONE violation PER CLASH, or <c>409</c> when the
/// exclusion constraint settles a race (the filter maps <c>23P01</c>). Never <c>202</c>. Reading a surgery is a clinical-record access and is logged (NFR-SEC-8) -- fail closed.
/// </summary>
[ApiController]
[Route("api/staff/surgeries")]
public sealed class StaffSurgeriesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : SurgeryControllerBase(activeBranchAccessor)
{
    private UserContext User => userContextProvider.GetUserContext();

    [HttpGet("services")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Services()
    {
        var response = await dispatcher.GetResultAsync(new GetSurgeryServicesQuery { UserContext = User, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("staff")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Staff([FromQuery] Guid serviceId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSurgeryStaffQuery { BranchId = branchId, ServiceId = serviceId, UserContext = User, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("availability")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Availability(
        [FromQuery] Guid serviceId, [FromQuery] DateOnly date, [FromQuery] Guid? surgeonId, [FromQuery] Guid? assistantId, [FromQuery] Guid? theatreId,
        [FromQuery] Guid? catId, [FromQuery] Guid? excludeSurgeryId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSurgeryAvailabilityQuery
        {
            BranchId = branchId, ServiceId = serviceId, Date = date, SurgeonId = surgeonId, AssistantId = assistantId, TheatreId = theatreId, CatId = catId,
            ExcludeSurgeryId = excludeSurgeryId, UserContext = User, CorrelationId = Guid.NewGuid()
        });
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Surgery.Schedule)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> Book([FromBody] BookSurgeryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var surgeryId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new BookSurgeryCommand
        {
            SurgeryId = surgeryId, AppointmentId = Guid.NewGuid(), VisitId = Guid.NewGuid(), BranchId = branchId, CustomerId = request.CustomerId, CatId = request.CatId,
            ServiceId = request.ServiceId, StartsOn = request.StartsOn, TheatreId = request.TheatreId, SurgeonId = request.SurgeonId, AssistantId = request.AssistantId,
            CampaignId = request.CampaignId, CampaignEligibilityNote = request.CampaignEligibilityNote, ConfirmNeuterStatusDiscrepancy = request.ConfirmNeuterStatusDiscrepancy,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { surgeryId }) : RespondToFailure(response);
    }

    [HttpGet]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status, [FromQuery] Guid? surgeonId, [FromQuery] string? pricingBasis,
        [FromQuery] bool? reportOutstanding, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var query = new GetSurgeryListQuery
        {
            BranchId = branchId, From = from, To = to, Status = status, SurgeonId = surgeonId, PricingBasis = pricingBasis, ReportOutstanding = reportOutstanding,
            UserContext = User, CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("day")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Day([FromQuery] DateOnly date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSurgeryDayListQuery { BranchId = branchId, Date = date, UserContext = User, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Detail(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSurgeryDetailQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        if (response.Result is null) return NotFound();

        // NFR-SEC-8: the cat is only known after the read, so the access row is written here; fail closed -- nothing clinical is released unlogged.
        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", response.Result.CatId, id)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    /// <summary>US-SRG-05, US-SRG-06 -- the procedure ends (Completed or Abandoned). Vet only. A Completed spay or neuter sets the cat neuter status through the Cat context.</summary>
    [HttpPost("{id:guid}/end-procedure")]
    [RequireAction(Actions.Surgery.EndProcedure)]
    public async Task<IActionResult> EndProcedure(Guid id, [FromBody] EndSurgicalProcedureRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new EndSurgicalProcedureCommand
        { SurgeryId = id, BranchId = branchId, Outcome = request.Outcome, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    /// <summary>US-SRG-06 -- the repair path for the neuter status (idempotent). A deceased cat is refused plainly with <c>CatIsDeceased</c>.</summary>
    [HttpPost("{id:guid}/apply-sterilisation-outcome")]
    [RequireAction(Actions.Surgery.WriteReport)]
    public async Task<IActionResult> ApplySterilisationOutcome(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApplySterilisationOutcomeCommand
        { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    [HttpPut("{id:guid}/recovery-status")]
    [RequireAction(Actions.Surgery.ManageRecovery)]
    public async Task<IActionResult> SetRecoveryStatus(Guid id, [FromBody] SetRecoveryStatusRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetRecoveryStatusCommand
        { SurgeryId = id, BranchId = branchId, Status = request.Status, Note = request.Note, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    /// <summary>US-SRG-10 -- polled every 30 s by the front desk; one projection, no per-row lookups. Not a record access: it lists names and phone numbers, not clinical content.</summary>
    [HttpGet("recovery-board")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> RecoveryBoard()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetRecoveryBoardQuery { BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}/discharge-draft")]
    [RequireAction(Actions.Surgery.ManageRecovery)]
    public async Task<IActionResult> DischargeDraft(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDischargeDraftQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{id:guid}/discharge")]
    [RequireAction(Actions.Surgery.ManageRecovery)]
    public async Task<IActionResult> Discharge(Guid id, [FromBody] DischargeSurgeryRequest? request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new DischargeSurgeryCommand
        {
            SurgeryId = id, BranchId = branchId, PostOpInstructions = request?.PostOpInstructions,
            FollowUps = request?.FollowUps?.Select(f => new DischargeFollowUpInput(f.Kind, f.DueOn)).ToList(),
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        if (!response.IsSuccess) return RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);

        var detail = await dispatcher.GetResultAsync(new GetSurgeryDetailQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });

        // NFR-SEC-8: the read-back is a clinical read of the full detail, so it writes its access row like GET detail does (fail closed: the change is already committed, the detail is withheld).
        if (detail.Result is not null && !await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", detail.Result.CatId, id)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(new { surgery = detail.Result });
    }

    [HttpPost("{id:guid}/follow-ups/{followUpId:guid}/complete")]
    [RequireAction(Actions.Surgery.ManageRecovery)]
    public async Task<IActionResult> CompleteFollowUp(Guid id, Guid followUpId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CompleteSurgeryFollowUpCommand
        { SurgeryId = id, BranchId = branchId, FollowUpId = followUpId, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound, BusinessViolationCodes.SurgeryFollowUpNotFound);
    }

    [HttpPost("{id:guid}/follow-ups/{followUpId:guid}/cancel")]
    [RequireAction(Actions.Surgery.ManageRecovery)]
    public async Task<IActionResult> CancelFollowUp(Guid id, Guid followUpId, [FromBody] CancelSurgeryFollowUpRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelSurgeryFollowUpCommand
        { SurgeryId = id, BranchId = branchId, FollowUpId = followUpId, Reason = request.Reason, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound, BusinessViolationCodes.SurgeryFollowUpNotFound);
    }

    [HttpPost("{id:guid}/reschedule")]
    [RequireAction(Actions.Surgery.Schedule)]
    [SlotClaimEndpoint]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleSurgeryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RescheduleSurgeryCommand
        {
            SurgeryId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, StartsOn = request.StartsOn, TheatreId = request.TheatreId,
            SurgeonId = request.SurgeonId, AssistantId = request.AssistantId, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        if (!response.IsSuccess) return RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);

        var detail = await dispatcher.GetResultAsync(new GetSurgeryDetailQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });

        // NFR-SEC-8: the read-back is a clinical read of the full detail, so it writes its access row like GET detail does (fail closed: the change is already committed, the detail is withheld).
        if (detail.Result is not null && !await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", detail.Result.CatId, id)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(new { surgery = detail.Result });
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Surgery.Schedule)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelSurgeryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelSurgeryCommand
        { SurgeryId = id, BranchId = branchId, Reason = request.Reason, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    [HttpPost("{id:guid}/checklist/{kind}/complete")]
    [RequireAction(Actions.Surgery.ManageChecklist)]
    public async Task<IActionResult> CompleteChecklistItem(Guid id, string kind, [FromBody] CompleteChecklistItemRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CompleteChecklistItemCommand
        {
            SurgeryId = id, BranchId = branchId, Kind = kind, Note = request.Note, WeightRecordId = request.WeightRecordId,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    [HttpPost("{id:guid}/checklist/{kind}/waive")]
    [RequireAction(Actions.Surgery.AssessRisk)]
    public async Task<IActionResult> WaiveChecklistItem(Guid id, string kind, [FromBody] WaiveChecklistItemRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new WaiveChecklistItemCommand
        { SurgeryId = id, BranchId = branchId, Kind = kind, Reason = request.Reason, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    [HttpPut("{id:guid}/asa-grade")]
    [RequireAction(Actions.Surgery.AssessRisk)]
    public async Task<IActionResult> RecordAsaGrade(Guid id, [FromBody] RecordAsaGradeRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordAsaGradeCommand
        { SurgeryId = id, BranchId = branchId, Grade = request.Grade, IsEmergency = request.IsEmergency, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    [HttpPost("{id:guid}/admit")]
    [RequireAction(Actions.Surgery.Admit)]
    public async Task<IActionResult> Admit(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AdmitSurgeryCommand
        { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    /// <summary>The ordinary start. A body is not read: this route can never carry an override (<c>start-with-consent-override</c> does), so a nurse's start can never bypass consent.</summary>
    [HttpPost("{id:guid}/start")]
    [RequireAction(Actions.Surgery.Start)]
    public async Task<IActionResult> Start(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new StartSurgeryCommand
        { SurgeryId = id, BranchId = branchId, ConsentOverrideReason = null, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);
    }

    /// <summary>BR-5 -- a veterinarian's emergency override. <c>[RequireAction(Surgery.OverrideConsent)]</c> refuses anyone else with a <c>403</c> and an <c>AuthorizationDenied</c>
    /// audit row; the handler checks the role again. The reason is stored on the surgery and written to the audit log with the vet's identity.</summary>
    [HttpPost("{id:guid}/start-with-consent-override")]
    [RequireAction(Actions.Surgery.OverrideConsent)]
    public async Task<IActionResult> StartWithConsentOverride(Guid id, [FromBody] StartWithConsentOverrideRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new StartSurgeryCommand
        { SurgeryId = id, BranchId = branchId, ConsentOverrideReason = request.Reason, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        if (!response.IsSuccess) return RespondToFailure(response, BusinessViolationCodes.SurgeryNotFound);

        return response.Result is StartSurgeryResult { OverrideIgnored: true } ? Ok(new { consentWasOverridden = false, overrideIgnored = true }) : NoContent();
    }
}
