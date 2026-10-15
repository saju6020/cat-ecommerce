using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.CommandHandlers.Boarding;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 -- staff booking and stay reads. The branch is the active branch (<c>X-Branch-Id</c>); a stay of another branch is <c>404</c>.</summary>
[ApiController]
[Route("api/staff/boarding")]
public sealed class StaffBoardingStaysController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpGet("availability")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] DateOnly arrivalOn, [FromQuery] DateOnly departureOn, [FromQuery] int catCount, [FromQuery] bool requiresIsolation, [FromQuery] Guid? excludeStayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingAvailabilityQuery
        {
            BranchId = branchId, ArrivalOn = arrivalOn, DepartureOn = departureOn, CatCount = catCount, IsStaff = true, RequiresIsolation = requiresIsolation,
            ExcludeStayId = excludeStayId, UserContext = Caller, CorrelationId = Guid.NewGuid()
        });
        return Ok(response.Result);
    }

    [HttpGet("occupancy")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetOccupancy([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 30) return BadRequest(new { error = "The range cannot exceed 31 days." });
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetBoardingOccupancyQuery { BranchId = branchId, From = from, To = to, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("enquiries")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> CreateEnquiry([FromBody] CreateStaffBoardingEnquiryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CreateBoardingEnquiryCommand
        {
            StayId = Guid.NewGuid(), BranchId = branchId, CustomerId = request.CustomerId, IsStaff = true, ArrivalOn = request.ArrivalOn, DepartureOn = request.DepartureOn,
            UnitType = request.UnitType, CatIds = request.CatIds, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, EnquiryBody((EnquiryResult)response.Result)));
    }

    [HttpPut("stays/{stayId:guid}/enquiry")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> ChangeEnquiry(Guid stayId, [FromBody] ChangeBoardingEnquiryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ChangeBoardingEnquiryCommand
        {
            StayId = stayId, BranchId = branchId, ArrivalOn = request.ArrivalOn, DepartureOn = request.DepartureOn, UnitType = request.UnitType, CatIds = request.CatIds,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => Ok(EnquiryBody((EnquiryResult)response.Result)));
    }

    [HttpPut("stays/{stayId:guid}/cats/{catId:guid}/care-profile")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> SetCareProfile(Guid stayId, Guid catId, [FromBody] CareProfileRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryMapCareProfile(request, out var input, out var error)) return BadRequest(new { error });

        var response = await Dispatcher.SendAsync(new SetStayCareProfileCommand
        { StayId = stayId, CatId = catId, BranchId = branchId, Profile = input!, VetAuthorityChangeReason = request.VetAuthorityChangeReason, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/cats/{catId:guid}/vaccination-override")]
    [RequireAction(Actions.Boarding.OverrideVaccination)]
    public async Task<IActionResult> OverrideVaccination(Guid stayId, Guid catId, [FromBody] OverrideVaccinationRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new OverrideVaccinationGateCommand
        { StayId = stayId, CatId = catId, BranchId = branchId, Reason = request.Reason, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/reserve")]
    [RequireAction(Actions.Boarding.Book)]
    [BoardingClaimEndpoint]
    public async Task<IActionResult> Reserve(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ReserveBoardingStayCommand
        { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (ReserveResult)response.Result;
        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(new
        {
            stay = detail.Result,
            deposit = new
            {
                amount = new { amount = result.DepositAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), currencyCode = result.CurrencyCode },
                payBy = result.PayBy,
                instructionsPath = "/pages/boarding-payment-instructions",
            },
        });
    }

    [HttpPost("stays/{stayId:guid}/confirm")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> Confirm(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new ConfirmBoardingStayCommand
        { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, () =>
        {
            var result = (ConfirmResult)response.Result;
            return Ok(new
            {
                status = result.Status, depositState = result.DepositState,
                outstanding = new { amount = result.Outstanding.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), currencyCode = result.CurrencyCode },
            });
        });
    }

    [HttpPost("stays/{stayId:guid}/cancel")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> Cancel(Guid stayId, [FromBody] CancelBoardingStayRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CancelBoardingStayCommand
        { StayId = stayId, BranchId = branchId, Note = request.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, () => Ok(new { depositForfeited = ((CancelResult)response.Result).DepositForfeited }));
    }

    [HttpPost("stays/{stayId:guid}/no-show")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> NoShow(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordBoardingNoShowCommand
        { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("stays")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetStays(
        [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? customerId, [FromQuery] Guid? catId,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var query = new GetStaysQuery
        {
            BranchId = branchId, Status = status, From = from, To = to, CustomerId = customerId, CatId = catId, UserContext = Caller, CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = Math.Min(pageSize.Value, 100);

        var response = await Dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("stays/{stayId:guid}")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> GetStay(Guid stayId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
