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
/// HO-EMR-001 §12.4 -- consent for surgery, anaesthesia and euthanasia (US-CLN-21, ADR-132). <c>Consents.View</c> /
/// <c>Consents.Record</c> are open to Reception, Nurse, Vet and Administrator: reception captures the owner's signature at the
/// desk. A consent is linked to the cat, the procedure booking (an appointment on a theatre room), the kind, the date and the exact
/// consent-form version; it is never edited or deleted, only withdrawn. Reads of a recorded consent are access-logged (§8.11);
/// <c>GET .../status</c> is the contract HO-SRG-001 reads to refuse "procedure started" without a valid consent (AC2).
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffConsentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet("consent-forms/{kind}")]
    [RequireAction(Actions.Consents.View)]
    public async Task<IActionResult> GetConsentForm(string kind)
    {
        var response = await dispatcher.GetResultAsync(new GetConsentFormQuery { Kind = kind, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("consents")]
    [RequireAction(Actions.Consents.Record)]
    public async Task<IActionResult> Record([FromBody] RecordConsentRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var consentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RecordConsentCommand
        {
            ConsentId = consentId, CatId = request.CatId, AppointmentId = request.AppointmentId, BranchId = branchId, Kind = request.Kind,
            ProcedureDescription = request.ProcedureDescription, Method = request.Method, Signature = request.Signature, FileId = request.FileId,
            SignedByName = request.SignedByName, SignerRelationship = request.SignerRelationship, SignedByCustomerId = request.SignedByCustomerId,
            PolicyVersionNumber = request.PolicyVersionNumber, WitnessStaffId = request.WitnessStaffId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { consentId }) : ClinicalResponses.Failure(this, response);
    }

    [HttpGet("consents/{consentId:guid}")]
    [RequireAction(Actions.Consents.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.Consent))]
    public async Task<IActionResult> GetConsent(Guid consentId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetConsentQuery
        {
            ConsentId = consentId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("cats/{catId:guid}/consents")]
    [RequireAction(Actions.Consents.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.Consent))]
    public async Task<IActionResult> GetCatConsents(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetCatConsentsQuery { CatId = catId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    /// <summary>The signature drawn as polylines from stored numbers -- never an uploaded image. Served with a CSP that forbids
    /// everything, so even a defect upstream could not make the response execute anything.</summary>
    [HttpGet("consents/{consentId:guid}/signature.svg")]
    [RequireAction(Actions.Consents.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.Consent))]
    public async Task<IActionResult> GetSignature(Guid consentId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetConsentSignatureQuery
        {
            ConsentId = consentId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });
        if (response.Result is null) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return Content(response.Result, "image/svg+xml");
    }

    [HttpGet("consents/status")]
    [RequireAction(Actions.Consents.View)]
    public async Task<IActionResult> GetStatus([FromQuery] Guid catId, [FromQuery] Guid appointmentId, [FromQuery] string kind)
    {
        var response = await dispatcher.GetResultAsync(new GetConsentStatusQuery
        {
            CatId = catId, AppointmentId = appointmentId, Kind = kind, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPost("consents/{consentId:guid}/withdraw")]
    [RequireAction(Actions.Consents.Record)]
    public async Task<IActionResult> Withdraw(Guid consentId, [FromBody] WithdrawConsentRequest request)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new WithdrawConsentCommand
        {
            ConsentId = consentId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : ClinicalResponses.Failure(this, response);
    }
}
