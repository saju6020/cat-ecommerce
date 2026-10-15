using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Visit;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- diagnostic orders, results and lab files (US-CLN-14). Branch from <c>X-Branch-Id</c>; another
/// branch's order is a <c>404</c>. Writes need <c>Visits.WriteNote</c> (Vet); reads <c>Visits.View</c> and are access-logged
/// (§8.11). Results and files may arrive after the visit is finalised (§8.5); they are append-only.
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffDiagnosticsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpPost("visits/{visitId:guid}/diagnostics")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> Order(Guid visitId, [FromBody] OrderDiagnosticRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var diagnosticOrderId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = diagnosticOrderId, VisitId = visitId, BranchId = branchId, Kind = request.Kind, Description = request.Description,
            AnalyteIds = request.AnalyteIds, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { diagnosticOrderId }) : ClinicalResponses.Failure(this, response);
    }

    [HttpGet("diagnostics/{orderId:guid}")]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DiagnosticOrder))]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDiagnosticOrderQuery
        {
            DiagnosticOrderId = orderId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("diagnostics")]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.VisitList))]
    public async Task<IActionResult> GetPending([FromQuery] string status = "Pending")
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;
        if (!EnumerationParsing.TryParse<DiagnosticOrderStatus>(status, out _))
            return BadRequest(new { error = $"'{status}' is not a recognised diagnostic order status." });

        var response = await dispatcher.GetResultAsync(new GetPendingDiagnosticsQuery
        {
            BranchId = branchId, Status = status, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPost("diagnostics/{orderId:guid}/results")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> RecordResults(Guid orderId, [FromBody] RecordDiagnosticResultsRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordDiagnosticResultsCommand
        {
            DiagnosticOrderId = orderId, ResultSetId = Guid.NewGuid(), BranchId = branchId, ReportedOn = request.ReportedOn,
            Values = request.Values.Select(v => new ResultValueInput
            {
                ValueId = v.ValueId ?? Guid.NewGuid(), AnalyteId = v.AnalyteId, Name = v.Name, ValueKind = v.ValueKind, NumericValue = v.NumericValue,
                TextValue = v.TextValue, Unit = v.Unit, ReferenceLow = v.ReferenceLow, ReferenceHigh = v.ReferenceHigh, NormalText = v.NormalText
            }).ToList(),
            Note = request.Note, SupersedesResultSetId = request.SupersedesResultSetId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return ClinicalResponses.Failure(this, response);

        var result = (RecordResultsResult)response.Result;
        return StatusCode(201, new { resultSetId = result.ResultSetId, values = result.Values });
    }

    [HttpPost("diagnostics/{orderId:guid}/attachments")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> AttachFile(Guid orderId, [FromBody] AttachDiagnosticFileRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var attachmentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AttachDiagnosticFileCommand
        {
            DiagnosticOrderId = orderId, AttachmentId = attachmentId, BranchId = branchId, FileId = request.FileId,
            ResultDate = request.ResultDate, Note = request.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { attachmentId }) : ClinicalResponses.Failure(this, response);
    }

    [HttpPost("diagnostics/{orderId:guid}/cancel")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> Cancel(Guid orderId, [FromBody] CancelDiagnosticOrderRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelDiagnosticOrderCommand
        {
            DiagnosticOrderId = orderId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }
}
