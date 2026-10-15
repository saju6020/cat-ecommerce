using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Visit;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- staff visits. Branch from <c>X-Branch-Id</c> (HO-ADM-001 §8.3); a visit of another
/// branch is a <c>404</c>. Every endpoint that returns clinical data carries
/// <c>[ClinicalRecordAccess]</c> (§8.11, ADR-129). No write path exists for a finalised visit: the aggregate
/// refuses it (<c>VisitFinalised</c> -> <c>400</c>) and a database trigger backstops it (<c>CR001</c> -> <c>409</c>).
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffVisitsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor,
    IDischargeSummaryPdfRenderer pdfRenderer) : ControllerBase
{
    [HttpPost("visits")]
    [RequireAction(Actions.Visits.Open)]
    public async Task<IActionResult> Open([FromBody] OpenVisitRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new OpenVisitCommand
        {
            VisitId = Guid.NewGuid(), AppointmentId = request.AppointmentId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return ClinicalResponses.Failure(this, response);

        var result = (OpenVisitResult)response.Result;
        return result.AlreadyExisted ? Ok(new { visitId = result.VisitId }) : StatusCode(201, new { visitId = result.VisitId });
    }

    [HttpGet("appointments/{appointmentId:guid}/visit")]
    [RequireAction(Actions.Visits.View)]
    public async Task<IActionResult> GetByAppointment(Guid appointmentId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetVisitByAppointmentQuery
        {
            AppointmentId = appointmentId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("visits")]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.VisitList))]
    public async Task<IActionResult> GetVisits([FromQuery] string status = "Open", [FromQuery] DateOnly? date = null)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (!EnumerationParsing.TryParse<VisitStatus>(status, out _))
            return BadRequest(new { error = $"'{status}' is not a recognised visit status." });

        var response = await dispatcher.GetResultAsync(new GetOpenVisitsQuery
        {
            BranchId = branchId, Status = status, Date = date, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("visits/{visitId:guid}")]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.Visit))]
    public async Task<IActionResult> GetVisit(Guid visitId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetVisitDetailQuery
        {
            VisitId = visitId, BranchId = branchId, CallerRoles = ClinicalResponses.CallerRoles(this),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("clinical/vital-sign-rules")]
    [RequireAction(Actions.Visits.View)]
    public async Task<IActionResult> GetVitalSignRules()
    {
        var response = await dispatcher.GetResultAsync(new GetVitalSignRulesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("visits/{visitId:guid}/vitals")]
    [RequireAction(Actions.Visits.RecordVitals)]
    public async Task<IActionResult> RecordVitals(Guid visitId, [FromBody] RecordVitalsRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var vitalsEntryId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RecordVitalsCommand
        {
            VisitId = visitId, VitalsEntryId = vitalsEntryId, BranchId = branchId, MeasuredOn = request.MeasuredOn,
            WeightKg = request.WeightKg, TemperatureC = request.TemperatureC, HeartRateBpm = request.HeartRateBpm,
            RespiratoryRatePerMin = request.RespiratoryRatePerMin, BodyConditionScore = request.BodyConditionScore, PainScore = request.PainScore,
            ConfirmedOutOfRange = request.ConfirmedOutOfRange, Note = request.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess) return StatusCode(201, new { vitalsEntryId });

        // §8.3: a plausible-range refusal also carries each unconfirmed sign with the range it was judged against.
        if (response.Result is IReadOnlyList<OutOfRangeSign> outOfRange)
            return ClinicalResponses.FailureWith(this, response.ValidationResult, "outOfRange", outOfRange);

        return ClinicalResponses.Failure(this, response);
    }

    /// <summary>US-CLN-12 AC2 -- the author's auto-saved working copy. A race of two first writes by the same author hits the unique
    /// <c>(visit_id, author_staff_id)</c> index; <c>UniqueConstraintExceptionFilter</c> answers it by re-dispatching once as an
    /// overwrite (§12.5), so this action only dispatches and returns.</summary>
    [HttpPut("visits/{visitId:guid}/draft")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> SaveDraft(Guid visitId, [FromBody] SaveConsultationDraftRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SaveConsultationDraftCommand
        {
            VisitId = visitId, DraftId = Guid.NewGuid(), BranchId = branchId, Subjective = request.Subjective, Objective = request.Objective,
            Assessment = request.Assessment, Plan = request.Plan, DischargeInstructions = request.DischargeInstructions,
            ProblemCodeIds = request.ProblemCodeIds, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }

    [HttpPut("visits/{visitId:guid}/note")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> SaveNote(Guid visitId, [FromBody] SaveConsultationNoteRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SaveConsultationNoteCommand
        {
            VisitId = visitId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion,
            Subjective = request.Subjective, Objective = request.Objective, Assessment = request.Assessment, Plan = request.Plan,
            DischargeInstructions = request.DischargeInstructions,
            Problems = request.Problems.Select(p => new ProblemInput
            {
                ProblemId = p.ProblemId ?? Guid.NewGuid(), DiagnosisCodeId = p.DiagnosisCodeId, Certainty = p.Certainty, Note = p.Note
            }).ToList(),
            AppliedTemplateId = request.AppliedTemplateId, AppliedTemplateVersion = request.AppliedTemplateVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return ClinicalResponses.Failure(this, response);

        var result = (SaveNoteResult)response.Result;
        return Ok(new { version = result.Version, savedOn = result.SavedOn });
    }

    /// <summary>US-CLN-17 / US-CLN-22 -- <c>Visits.Finalise</c> maps to <c>Vet</c> alone (ADR-119): any other role is
    /// refused by the authorisation filter with <c>403</c> and an <c>AuthorizationDenied</c> audit row, before this
    /// action runs; the aggregate re-checks the role.</summary>
    [HttpPost("visits/{visitId:guid}/finalise")]
    [RequireAction(Actions.Visits.Finalise)]
    public async Task<IActionResult> Finalise(Guid visitId, [FromBody] FinaliseVisitRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new FinaliseVisitCommand
        {
            VisitId = visitId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }

    /// <summary>US-CLN-18 -- sets or replaces the follow-up: a link to an appointment booked through HO-APT-001, or a
    /// reminder queued with a send-time guard.</summary>
    [HttpPut("visits/{visitId:guid}/follow-up")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> SetFollowUp(Guid visitId, [FromBody] SetFollowUpRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetFollowUpCommand
        {
            VisitId = visitId, BranchId = branchId, Kind = request.Kind, DueOn = request.DueOn, AppointmentId = request.AppointmentId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }

    [HttpDelete("visits/{visitId:guid}/follow-up")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> ClearFollowUp(Guid visitId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ClearFollowUpCommand
        {
            VisitId = visitId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }

    /// <summary>US-CLN-16 -- the derived discharge summary of a <b>finalised</b> visit (an open one is a <c>404</c>).
    /// <c>Visits.PrintSummary</c>: Nurse, Vet, Administrator -- reception is not a clinical reader (OQ-5).</summary>
    [HttpGet("visits/{visitId:guid}/discharge-summary")]
    [RequireAction(Actions.Visits.PrintSummary)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DischargeSummary))]
    public async Task<IActionResult> GetDischargeSummary(Guid visitId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDischargeSummaryQuery
        {
            VisitId = visitId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("visits/{visitId:guid}/discharge-summary.pdf")]
    [RequireAction(Actions.Visits.PrintSummary)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DischargeSummary))]
    public async Task<IActionResult> GetDischargeSummaryPdf(Guid visitId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDischargeSummaryQuery
        {
            VisitId = visitId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : ClinicalResponses.Pdf(this, pdfRenderer, response.Result);
    }

    /// <summary>US-CLN-19 -- a dated, noted clinical photograph on an open visit. The file was registered by the upload
    /// endpoint (HO-FIL-001); this request names it by id.</summary>
    [HttpPost("visits/{visitId:guid}/photos")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> AttachPhoto(Guid visitId, [FromBody] AttachClinicalPhotoRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var photoId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AttachClinicalPhotoCommand
        {
            VisitId = visitId, PhotoId = photoId, BranchId = branchId, FileId = request.FileId, TakenOn = request.TakenOn, Note = request.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { photoId }) : ClinicalResponses.Failure(this, response);
    }

    /// <summary>US-CLN-15 -- records a procedure, medicine or consumable. A medicine or consumable takes the exact batch from the active branch's
    /// clinic dispensary in the same commit (ADR-126); the 400/409 shapes are in <see cref="ClinicalResponses.Failure"/>. <c>Visits.WriteNote</c> is
    /// Vet alone -- a nurse records vitals, not treatments (OQ-4).</summary>
    [HttpPost("visits/{visitId:guid}/treatments")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> RecordTreatment(Guid visitId, [FromBody] RecordTreatmentRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordTreatmentCommand
        {
            VisitId = visitId, LineId = request.LineId is { } lineId && lineId != Guid.Empty ? lineId : Guid.NewGuid(), BranchId = branchId, Kind = request.Kind, Description = request.Description,
            ServiceId = request.ServiceId, SkuId = request.SkuId, BatchId = request.BatchId, Quantity = request.Quantity,
            Dose = request.Dose, Route = request.Route, IsBillable = request.IsBillable, AcceptShortfall = request.AcceptShortfall,
            BatchOverrideReason = request.BatchOverrideReason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return ClinicalResponses.Failure(this, response);

        var result = (RecordTreatmentResult)response.Result;
        // 201 for a new treatment, 200 when the client's line id had already been recorded (an idempotent retry): same body, nothing deducted again.
        var body = new
        {
            lineId = result.LineId,
            deductedQuantity = InventoryViewFormat.Quantity(result.DeductedQuantity),
            shortfallQuantity = InventoryViewFormat.Quantity(result.ShortfallQuantity),
            unit = result.Unit,
            batch = result.Batch is null ? null : new { id = result.Batch.Id, lotNumber = result.Batch.LotNumber, expiresOn = result.Batch.ExpiresOn },
            lines = result.Lines.Select(l => new
            {
                lineId = l.LineId,
                quantity = InventoryViewFormat.Quantity(l.Quantity),
                deductedQuantity = InventoryViewFormat.Quantity(l.DeductedQuantity),
                shortfallQuantity = InventoryViewFormat.Quantity(l.ShortfallQuantity),
                batch = l.Batch is null ? null : new { id = l.Batch.Id, lotNumber = l.Batch.LotNumber, expiresOn = l.Batch.ExpiresOn }
            }).ToList()
        };

        return result.Replayed ? Ok(body) : StatusCode(201, body);
    }

    /// <summary>US-CLN-15 -- reverses a treatment line of an open visit; the line stays, marked reversed, and any stock it took is returned by a
    /// compensating movement committed with it.</summary>
    [HttpPost("visits/{visitId:guid}/treatments/{lineId:guid}/reverse")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> ReverseTreatment(Guid visitId, Guid lineId, [FromBody] ReverseTreatmentRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReverseTreatmentLineCommand
        {
            VisitId = visitId, LineId = lineId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }

    [HttpPost("visits/{visitId:guid}/addenda")]
    [RequireAction(Actions.Visits.AddAddendum)]
    public async Task<IActionResult> AddAddendum(Guid visitId, [FromBody] AddVisitAddendumRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var addendumId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AddVisitAddendumCommand
        {
            AddendumId = addendumId, VisitId = visitId, BranchId = branchId, Text = request.Text, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { addendumId }) : ClinicalResponses.Failure(this, response);
    }
}
