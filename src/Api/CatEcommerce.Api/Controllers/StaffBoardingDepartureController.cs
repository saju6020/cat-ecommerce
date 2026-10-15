using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 (Phase G) -- referral, vet approvals, extension, relocation, check-out and the stay summary. Staff; the branch is the active branch.</summary>
[ApiController]
[Route("api/staff/boarding")]
public sealed class StaffBoardingDepartureController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpPost("stays/{stayId:guid}/referrals")]
    [RequireAction(Actions.Boarding.Refer)]
    public async Task<IActionResult> Refer(Guid stayId, [FromBody] ReferCatRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ReferStayCatToClinicCommand
        { StayId = stayId, CatId = request.CatId, BranchId = branchId, Reason = request.Reason, Urgency = request.Urgency, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, () => StatusCode(201, new { referralId = (Guid)response.Result }));
    }

    [HttpPost("stays/{stayId:guid}/referrals/{referralId:guid}/return")]
    [RequireAction(Actions.Boarding.Refer)]
    public async Task<IActionResult> ReturnFromClinic(Guid stayId, Guid referralId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordReferralReturnCommand
        { StayId = stayId, ReferralId = referralId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/vet-approvals")]
    [RequireAction(Actions.Boarding.RequestVetApproval)]
    public async Task<IActionResult> RequestApproval(Guid stayId, [FromBody] RequestVetApprovalRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryParseMoney(request.Amount, out var amount)) return BadRequest(new { error = "The amount is not a number." });

        var response = await Dispatcher.SendAsync(new RequestVetChargeApprovalCommand
        {
            StayId = stayId, CatId = request.CatId, BranchId = branchId, ReferralId = request.ReferralId, Amount = amount, Description = request.Description,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { approvalId = (Guid)response.Result }));
    }

    [HttpPost("stays/{stayId:guid}/vet-approvals/{approvalId:guid}/staff-answer")]
    [RequireAction(Actions.Boarding.RequestVetApproval)]
    public async Task<IActionResult> StaffAnswer(Guid stayId, Guid approvalId, [FromBody] VetApprovalAnswerRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordVetChargeApprovalByStaffCommand
        {
            StayId = stayId, ApprovalId = approvalId, BranchId = branchId, Approve = request.Approve, Note = request.Note ?? "", UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, NoContent);
    }

    [HttpGet("stays/{stayId:guid}/cats/{catId:guid}/vet-headroom")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> Headroom(Guid stayId, Guid catId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetVetHeadroomQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        var cat = response.Result?.FirstOrDefault(h => h.CatId == catId);
        return cat is null ? NotFound() : Ok(cat);
    }

    [HttpGet("stays/{stayId:guid}/summary")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> Summary(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetStaySummaryQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/extend")]
    [RequireAction(Actions.Boarding.ChangeStay)]
    [BoardingClaimEndpoint]
    public async Task<IActionResult> Extend(Guid stayId, [FromBody] ExtendStayRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ExtendBoardingStayCommand
        { StayId = stayId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, NewDepartureOn = request.NewDepartureOn, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return await RespondToExtensionFailureAsync(stayId, branchId, request.NewDepartureOn, response.ValidationResult);

        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(new { stay = detail.Result });
    }

    [HttpGet("stays/{stayId:guid}/relocation-options")]
    [RequireAction(Actions.Boarding.ChangeStay)]
    public async Task<IActionResult> RelocationOptions(Guid stayId, [FromQuery] DateOnly? newDepartureOn)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetRelocationOptionsQuery { StayId = stayId, BranchId = branchId, NewDepartureOn = newDepartureOn, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/relocate")]
    [RequireAction(Actions.Boarding.ChangeStay)]
    [BoardingClaimEndpoint]
    public async Task<IActionResult> Relocate(Guid stayId, [FromBody] RelocateStayRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RelocateBoardingStayCommand
        {
            StayId = stayId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, TargetUnitId = request.TargetUnitId, EffectiveFromLocalDate = request.EffectiveFromLocalDate,
            Reason = request.Reason, NewDepartureOn = request.NewDepartureOn, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(new { stay = detail.Result });
    }

    /// <summary>Owner-authorised 2026-10-14 ("Show before"): what an early check-out would charge and credit, without changing anything.</summary>
    [HttpGet("stays/{stayId:guid}/check-out-preview")]
    [RequireAction(Actions.Boarding.CheckInOut)]
    public async Task<IActionResult> EarlyCheckOutPreview(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetEarlyCheckoutPreviewQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/check-out")]
    [RequireAction(Actions.Boarding.CheckInOut)]
    public async Task<IActionResult> CheckOut(Guid stayId, [FromBody] CheckOutRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var cats = request.Cats.Select(c => new CheckOutCatInput(
            c.CatId, c.WeightKg, c.Belongings.Select(b => new BelongingReturnItem(b.BelongingId, b.Returned, b.Note)).ToList(),
            c.Signature is null ? null : new SignatureInput(c.Signature.SignerName, c.Signature.Method, c.Signature.Strokes?.GetRawText(), c.Signature.ConfirmedStatement))).ToList();

        var response = await Dispatcher.SendAsync(new CheckOutBoardingStayCommand
        { StayId = stayId, BranchId = branchId, Cats = cats, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(new { stay = detail.Result });
    }

    [HttpPost("stays/{stayId:guid}/attention/clear")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> ClearAttention(Guid stayId, [FromBody] ClearAttentionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ClearStayAttentionCommand
        { StayId = stayId, BranchId = branchId, Note = request.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    /// <summary>US-BRD-15 AC2: a refused extension carries <c>relocationOptions[]</c> -- the units free for the rest of the stay -- next to the violation.</summary>
    private async Task<IActionResult> RespondToExtensionFailureAsync(Guid stayId, Guid branchId, DateOnly newDepartureOn, Platform.Infrastructure.Core.Validation.ValidationResponse? validation)
    {
        var codes = validation?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        if (!codes.Contains(BusinessViolationCodes.UnitNotFreeForExtension)) return RespondToFailure(validation);

        var options = await Dispatcher.GetResultAsync(new GetRelocationOptionsQuery { StayId = stayId, BranchId = branchId, NewDepartureOn = newDepartureOn, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return BadRequest(new { errors = validation!.Errors, isValid = false, relocationOptions = options.Result ?? [] });
    }
}
