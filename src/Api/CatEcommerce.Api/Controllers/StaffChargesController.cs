using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BIL-001 §12.4 -- charges and the capture pipeline (US-BIL-02). Charges are scoped to the caller's
/// active branch; a charge of another branch is "not found" (HO-ADM-001 §8.2).</summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffChargesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpGet("charges")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetCharges(
        [FromQuery] Guid? customerId, [FromQuery] string? status, [FromQuery] string? sourceKind, [FromQuery] Guid? billingGroupId,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetChargesQuery
        {
            BranchId = branchId, CustomerId = customerId, Status = status, SourceKind = sourceKind, BillingGroupId = billingGroupId,
            PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpPost("charges")]
    [RequireAction(Actions.Billing.ManageCharges)]
    public async Task<IActionResult> Record([FromBody] RecordManualChargeRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var chargeId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RecordManualChargeCommand
        {
            ChargeId = chargeId, CustomerId = request.CustomerId, BranchId = branchId, CatId = request.CatId, ServiceId = request.ServiceId,
            Description = request.Description, UnitPrice = request.UnitPrice, TaxClassId = request.TaxClassId, Quantity = request.Quantity,
            LineDiscount = request.LineDiscount, ServiceDate = request.ServiceDate, BusinessLine = request.BusinessLine,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? StatusCode(201, new { chargeId }) : RespondToFailure(response.ValidationResult);
    }

    [HttpPost("charges/{chargeId:guid}/void")]
    [RequireAction(Actions.Billing.ManageCharges)]
    public async Task<IActionResult> Void(Guid chargeId, [FromBody] VoidChargeRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new VoidChargeCommand
        {
            ChargeId = chargeId, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, BusinessViolationCodes.ChargeNotFound);
    }

    [HttpGet("uncaptured-sources")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetUncapturedSources([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (to < from || to.DayNumber - from.DayNumber > BillingRules.MaxExportRangeDays)
            return BadRequest(new { error = $"The range must be at most {BillingRules.MaxExportRangeDays} days and not end before it starts." });

        var response = await dispatcher.GetResultAsync(new GetUncapturedSourcesQuery
        { BranchId = branchId, From = from, To = to, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost("sources/{sourceKind}/{sourceId:guid}/capture")]
    [RequireAction(Actions.Billing.ManageCharges)]
    public async Task<IActionResult> Capture(string sourceKind, Guid sourceId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CaptureSourceChargesCommand
        {
            SourceKind = sourceKind, SourceId = sourceId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? Ok(response.Result) : RespondToFailure(response.ValidationResult, BusinessViolationCodes.SourceNotFound);
    }
}
