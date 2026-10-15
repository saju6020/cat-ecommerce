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
/// HO-PHM-001 §12.4 (US-PHM-05) -- a dispensing once it exists: its detail with the FROZEN label, counted label prints (<c>Dispensing.Dispense</c>) and the same-day reversal
/// (<c>Dispensing.Reverse</c>: Vet, Administrator -- and, for a controlled medicine, a verified witness, so it carries the <c>witness</c> rate limit). A dispensing of another
/// branch is a <c>404</c>.
/// </summary>
[ApiController]
[Route("api/staff/dispensings")]
public sealed class StaffDispensingsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("{dispensingId:guid}")]
    [RequireAction(Actions.Dispensing.View)]
    public async Task<IActionResult> Get(Guid dispensingId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDispensingQuery
        { DispensingId = dispensingId, BranchId = branchId, CallerRoles = CallerRoles(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (response.Result is not { } dispensing) return NotFound();

        // NFR-SEC-8: a clinical read is access-logged through HO-EMR-001's command; fail closed.
        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", dispensing.CatId, dispensing.Id))
            return StatusCode(500, new { error = "The request could not be completed." });

        Response.Headers.CacheControl = "no-store";
        return Ok(dispensing);
    }

    [HttpPost("{dispensingId:guid}/label-prints")]
    [RequireAction(Actions.Dispensing.Dispense)]
    public async Task<IActionResult> RecordLabelPrint(Guid dispensingId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordLabelPrintCommand
        { DispensingId = dispensingId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.DispensingNotFound);
    }

    [HttpPost("{dispensingId:guid}/reverse")]
    [RequireAction(Actions.Dispensing.Reverse)]
    [EnableRateLimiting(PharmacyRateLimitPolicies.Witness)]
    public async Task<IActionResult> Reverse(Guid dispensingId, [FromBody] ReverseDispensingRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReverseDispensingCommand
        {
            DispensingId = dispensingId, Reason = request.Reason, WitnessIdentifier = request.WitnessIdentifier, WitnessPassword = request.WitnessPassword, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.DispensingNotFound);
    }
}
