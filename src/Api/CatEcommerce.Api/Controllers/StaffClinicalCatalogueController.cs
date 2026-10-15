using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- the three clinical catalogues: diagnosis codes, lab analytes, note templates.
/// Reads need <c>ClinicalCatalogue.View</c> (Nurse, Vet, Administrator); writes <c>ClinicalCatalogue.Manage</c>
/// (Vet, Administrator). Catalogue rows are never deleted -- only deactivated (C-ALL-4).
/// Catalogue reads are configuration, not clinical records, so they carry no access-log filter (U-X5's
/// explicit exception list).
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffClinicalCatalogueController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    // ── Diagnosis codes ────────────────────────────────────────────────────────────────────────

    [HttpGet("diagnosis-codes")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetDiagnosisCodes([FromQuery] string? search, [FromQuery] bool activeOnly = false, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetDiagnosisCodesQuery { Search = search, ActiveOnly = activeOnly, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("diagnosis-codes/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetDiagnosisCode(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetDiagnosisCodeQuery { DiagnosisCodeId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("diagnosis-codes")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> CreateDiagnosisCode([FromBody] CreateDiagnosisCodeRequest request)
    {
        var id = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateDiagnosisCodeCommand
        {
            DiagnosisCodeId = id, Code = request.Code, Name = request.Name, Category = request.Category,
            ExternalSystem = request.ExternalSystem, ExternalCode = request.ExternalCode,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { diagnosisCodeId = id }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("diagnosis-codes/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> UpdateDiagnosisCode(Guid id, [FromBody] UpdateDiagnosisCodeRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateDiagnosisCodeCommand
        {
            DiagnosisCodeId = id, Name = request.Name, Category = request.Category,
            ExternalSystem = request.ExternalSystem, ExternalCode = request.ExternalCode,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    [HttpPut("diagnosis-codes/{id:guid}/active")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> SetDiagnosisCodeActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var response = await dispatcher.SendAsync(new SetDiagnosisCodeActiveCommand
        {
            DiagnosisCodeId = id, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    // ── Lab analytes ───────────────────────────────────────────────────────────────────────────

    [HttpGet("lab-analytes")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetLabAnalytes([FromQuery] string? search, [FromQuery] bool activeOnly = false, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetLabAnalytesQuery { Search = search, ActiveOnly = activeOnly, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("lab-analytes/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetLabAnalyte(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetLabAnalyteQuery { LabAnalyteId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("lab-analytes")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> CreateLabAnalyte([FromBody] CreateLabAnalyteRequest request)
    {
        var id = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateLabAnalyteCommand
        {
            LabAnalyteId = id, Code = request.Code, Name = request.Name, ValueKind = request.ValueKind, DiagnosticKind = request.DiagnosticKind,
            Unit = request.Unit, ReferenceLow = request.ReferenceLow, ReferenceHigh = request.ReferenceHigh, NormalText = request.NormalText,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { labAnalyteId = id }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("lab-analytes/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> UpdateLabAnalyte(Guid id, [FromBody] UpdateLabAnalyteRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateLabAnalyteCommand
        {
            LabAnalyteId = id, Name = request.Name, ValueKind = request.ValueKind, DiagnosticKind = request.DiagnosticKind,
            Unit = request.Unit, ReferenceLow = request.ReferenceLow, ReferenceHigh = request.ReferenceHigh, NormalText = request.NormalText,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    [HttpPut("lab-analytes/{id:guid}/active")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> SetLabAnalyteActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var response = await dispatcher.SendAsync(new SetLabAnalyteActiveCommand
        {
            LabAnalyteId = id, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    // ── Note templates ─────────────────────────────────────────────────────────────────────────

    [HttpGet("note-templates")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetNoteTemplates([FromQuery] Guid? serviceId, [FromQuery] bool activeOnly = false, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetNoteTemplatesQuery { ServiceId = serviceId, ActiveOnly = activeOnly, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("note-templates/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.View)]
    public async Task<IActionResult> GetNoteTemplate(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetNoteTemplateQuery { NoteTemplateId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("note-templates")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> CreateNoteTemplate([FromBody] CreateNoteTemplateRequest request)
    {
        var id = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateNoteTemplateCommand
        {
            NoteTemplateId = id, ClinicServiceId = request.ServiceId, Name = request.Name, Subjective = request.Subjective,
            Objective = request.Objective, Assessment = request.Assessment, Plan = request.Plan, DischargeInstructions = request.DischargeInstructions,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { noteTemplateId = id }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("note-templates/{id:guid}")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> UpdateNoteTemplate(Guid id, [FromBody] UpdateNoteTemplateRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateNoteTemplateCommand
        {
            NoteTemplateId = id, Name = request.Name, Subjective = request.Subjective, Objective = request.Objective,
            Assessment = request.Assessment, Plan = request.Plan, DischargeInstructions = request.DischargeInstructions,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    [HttpPut("note-templates/{id:guid}/active")]
    [RequireAction(Actions.ClinicalCatalogue.Manage)]
    public async Task<IActionResult> SetNoteTemplateActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var response = await dispatcher.SendAsync(new SetNoteTemplateActiveCommand
        {
            NoteTemplateId = id, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response.IsSuccess, response.ValidationResult);
    }

    /// <summary>A "not found" violation is a 404; every other business violation a 400 (ADR-014).</summary>
    private IActionResult Respond(bool isSuccess, ValidationResponse? validationResult)
    {
        if (isSuccess) return NoContent();

        var codes = validationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        var notFound = codes.Contains(BusinessViolationCodes.DiagnosisCodeNotFound)
            || codes.Contains(BusinessViolationCodes.LabAnalyteNotFound)
            || codes.Contains(BusinessViolationCodes.NoteTemplateNotFound);

        return notFound ? NotFound() : BadRequest(validationResult);
    }
}
