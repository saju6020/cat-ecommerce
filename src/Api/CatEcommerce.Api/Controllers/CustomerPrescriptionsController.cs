using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Prescription;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-PHM-001 §12.4 (US-PHM-06, US-PHM-07, FR-POR-3) -- the owner's prescriptions in the portal. Every route resolves the prescription THROUGH its cat's CURRENT owner: anything that
/// is not the caller's is a <c>404</c>, with no hint that it exists (HO-ADM-001 §12.3). Requesting a repeat is rate limited (10 per hour per customer); outside the vet's window it
/// is a <c>400 PrescriptionValidityEnded</c> whose message says when the repeat ended, that a vet must authorise a new prescription, and carries the <c>bookLink</c> (US-PHM-06 AC2).
/// No batch number, safety record or staff detail is ever returned to an owner.
/// </summary>
[ApiController]
[RequireCustomerSession]
[Route("api/customers/me")]
public sealed class CustomerPrescriptionsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("prescriptions")]
    public async Task<IActionResult> GetPrescriptions([FromQuery] string? scope, [FromQuery] Guid? catId)
    {
        if (scope is not null && scope is not ("active" or "past")) return BadRequest(new { error = "scope must be 'active' or 'past'." });

        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerPrescriptionsQuery
        { CustomerId = user.UserId, Scope = scope ?? "active", CatId = catId, UserContext = user, CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("prescriptions/{prescriptionId:guid}")]
    public async Task<IActionResult> GetPrescription(Guid prescriptionId)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerPrescriptionDetailQuery
        { PrescriptionId = prescriptionId, CustomerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });

        if (response.Result is not { } detail) return NotFound();

        // NFR-SEC-8: the owner's read of a clinical record is access-logged too (the actor is the customer), failing closed.
        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", detail.CatId, detail.Id))
            return StatusCode(500, new { error = "The request could not be completed." });

        Response.Headers.CacheControl = "no-store";
        return Ok(detail);
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/repeat-requests")]
    [EnableRateLimiting(PharmacyRateLimitPolicies.RepeatRequest)]
    public async Task<IActionResult> RequestRepeat(Guid prescriptionId, [FromBody] RequestRepeatRequest request)
    {
        var requestId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RequestRepeatCommand
        {
            PrescriptionId = prescriptionId, RepeatRequestId = requestId, PreferredBranchId = request.PreferredBranchId, Note = request.Note,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess) return StatusCode(201, new { repeatRequestId = requestId });

        var extra = response.Result is RepeatRefusal refusal ? new Dictionary<string, object?> { ["bookLink"] = refusal.BookLink } : null;
        return RespondToFailure(response, extra, BusinessViolationCodes.PrescriptionNotFound);
    }

    [HttpDelete("prescriptions/{prescriptionId:guid}/repeat-requests/{requestId:guid}")]
    public async Task<IActionResult> WithdrawRepeat(Guid prescriptionId, Guid requestId)
    {
        var response = await dispatcher.SendAsync(new WithdrawRepeatRequestCommand
        {
            PrescriptionId = prescriptionId, RepeatRequestId = requestId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response);
    }
}
