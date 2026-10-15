using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Surgery;
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
/// HO-SRG-001 §12.4 (US-SRG-05, ADR-165, BR-6, BR-11) -- the surgical report. Reads are <c>Surgery.View</c> and are clinical-record accesses (NFR-SEC-8, fail closed); every write is
/// <c>Surgery.WriteReport</c> (Vet alone) and re-checked in the handler. <b>There is no way to change a finalised report</b>: a <c>PUT</c> after finalisation is <c>409 ReportNotEditable</c>,
/// and the only addition is an addendum (<c>201</c>). The cat and the visit it attaches to are on the read.
/// </summary>
[ApiController]
[Route("api/staff/surgeries/{id:guid}/report")]
public sealed class StaffSurgeryReportsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : SurgeryControllerBase(activeBranchAccessor)
{
    private UserContext User => userContextProvider.GetUserContext();

    [HttpGet]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetSurgicalReportQuery { SurgeryId = id, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        if (response.Result is null) return NotFound();

        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", response.Result.CatId, id)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpPut]
    [RequireAction(Actions.Surgery.WriteReport)]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveSurgicalReportDraftRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SaveSurgicalReportDraftCommand
        {
            SurgeryId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, ProcedurePerformed = request.ProcedurePerformed ?? "", Findings = request.Findings ?? "",
            Technique = request.Technique ?? "", SutureMaterial = request.SutureMaterial ?? "", HadComplications = request.HadComplications, Complications = request.Complications,
            SurgeonId = request.SurgeonId, AssistantId = request.AssistantId, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? Ok(new { version = ((SurgicalReportSavedResult)response.Result!).Version }) : RespondToFailure(response, RouteNotFoundCodes);
    }

    [HttpPost("finalise")]
    [RequireAction(Actions.Surgery.WriteReport)]
    public async Task<IActionResult> Finalise(Guid id, [FromBody] FinaliseSurgicalReportRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new FinaliseSurgicalReportCommand
        { SurgeryId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, RouteNotFoundCodes);
    }

    [HttpPost("addenda")]
    [RequireAction(Actions.Surgery.WriteReport)]
    public async Task<IActionResult> AddAddendum(Guid id, [FromBody] AddSurgicalReportAddendumRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var addendumId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AddSurgicalReportAddendumCommand
        { AddendumId = addendumId, SurgeryId = id, BranchId = branchId, Text = request.Text, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? StatusCode(201, new { addendumId }) : RespondToFailure(response, RouteNotFoundCodes);
    }
}
