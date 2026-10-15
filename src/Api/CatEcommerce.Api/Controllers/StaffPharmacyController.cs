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
/// HO-PHM-001 §12.4 (US-PHM-05, US-PHM-06 AC1) -- the pharmacy counter: the queue, the scanned-number lookup, the server's FEFO plan and the dispense itself. The branch is the
/// caller's active branch (<c>X-Branch-Id</c>). Dispensing is <c>Dispensing.Dispense</c> (Reception, Nurse, Vet, Administrator) and carries the <c>witness</c> rate limit because it
/// may accept witness credentials. An expired batch is refused with <c>400 BatchExpired</c> / <c>OnlyExpiredStockAvailable</c> and <b>no role, flag or parameter changes that</b>
/// (BR-3). A race on the last supply or the last unit is a <c>409</c> (the prescription's <c>xmin</c>, the supply index, the stock position's check).
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffPharmacyController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IClientIpAccessor clientIpAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("pharmacy/queue")]
    [RequireAction(Actions.Dispensing.View)]
    public async Task<IActionResult> GetQueue()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPharmacyQueueQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("pharmacy/lookup")]
    [RequireAction(Actions.Dispensing.View)]
    public async Task<IActionResult> Lookup([FromQuery] string? number)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Length > MaxNumberLength) return NotFound();

        var response = await dispatcher.GetResultAsync(new LookupPrescriptionByNumberQuery
        { Number = number, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    private const int MaxNumberLength = 20;

    [HttpGet("prescriptions/{prescriptionId:guid}/dispense-plan")]
    [RequireAction(Actions.Dispensing.Dispense)]
    public async Task<IActionResult> GetDispensePlan(Guid prescriptionId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDispensePlanQuery
        { PrescriptionId = prescriptionId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (response.Result is not { } plan) return NotFound();

        // NFR-SEC-8: the plan carries the prescription and the cat's header, so reading it is clinical access -- logged fail-closed, as prescription detail is.
        if (!await TryLogClinicalAccessAsync(dispatcher, userContextProvider, clientIpAccessor, Request.Path.Value ?? "", plan.Cat.Id, prescriptionId))
            return StatusCode(500, new { error = "The request could not be completed." });

        Response.Headers.CacheControl = "no-store";
        return Ok(plan);
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/dispensings")]
    [RequireAction(Actions.Dispensing.Dispense)]
    [EnableRateLimiting(PharmacyRateLimitPolicies.Witness)]
    public async Task<IActionResult> Dispense(Guid prescriptionId, [FromBody] DispensePrescriptionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var dispensingId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new DispensePrescriptionCommand
        {
            DispensingId = dispensingId, PrescriptionId = prescriptionId, BranchId = branchId,
            Lines = request.Lines.Select(l => new DispenseLineModel(l.SkuId, l.BatchId, l.Quantity)).ToList(),
            FefoOverrideReason = request.FefoOverrideReason, RepeatRequestId = request.RepeatRequestId, WitnessIdentifier = request.WitnessIdentifier,
            WitnessPassword = request.WitnessPassword, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return RespondToFailure(response, BusinessViolationCodes.PrescriptionNotFound);

        var dispensed = (DispensedResult)response.Result!;
        return StatusCode(201, new
        {
            dispensingId = dispensed.DispensingId, supplyNumber = dispensed.SupplyNumber, label = ToLabel(dispensed), unpricedLines = dispensed.UnpricedLineIds
        });
    }

    private static object ToLabel(DispensedResult d) => new
    {
        d.Label.CatName, d.Label.OwnerName, d.Label.MedicineLine, d.Label.DoseLine, d.Label.Instructions, dispensedOn = d.Label.DispensedOnLocal, d.Label.ExpiresOn,
        d.Label.QuantityLine, d.Label.PrescriptionNumber, d.Label.PrescriberName, d.Label.ClinicName, d.Label.ClinicPhone, d.Label.ClinicAddress, d.Label.ControlledNotice,
        d.Label.StandardWarnings
    };
}
