using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 (Phase H) -- the owner's waitlist and daycare. Someone else's entry or visit is <c>404</c>.</summary>
[ApiController]
[Route("api/customers/me")]
[RequireCustomerSession]
public sealed class CustomerDaycareController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    private Guid CustomerId => Caller.UserId;

    [HttpPost("boarding/waitlist")]
    [EnableRateLimiting(BoardingRateLimitPolicies.Booking)]
    public async Task<IActionResult> JoinWaitlist([FromBody] JoinWaitlistRequest request)
    {
        var entryId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new JoinBoardingWaitlistCommand
        {
            EntryId = entryId, BranchId = request.BranchId, CustomerId = CustomerId, ArrivalOn = request.ArrivalOn, DepartureOn = request.DepartureOn, CatCount = request.CatCount,
            UnitType = request.UnitType, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { entryId }));
    }

    [HttpGet("boarding/waitlist")]
    public async Task<IActionResult> GetWaitlist()
    {
        var response = await Dispatcher.GetResultAsync(new GetCustomerWaitlistQuery { CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpDelete("boarding/waitlist/{entryId:guid}")]
    public async Task<IActionResult> Withdraw(Guid entryId)
    {
        var response = await Dispatcher.SendAsync(new WithdrawBoardingWaitlistCommand { EntryId = entryId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, NoContent);
    }

    [HttpPost("daycare")]
    [EnableRateLimiting(BoardingRateLimitPolicies.Booking)]
    public async Task<IActionResult> Book([FromBody] BookDaycareRequest request)
    {
        var visitId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new BookDaycareVisitCommand
        {
            VisitId = visitId, BranchId = request.BranchId, CustomerId = CustomerId, CatId = request.CatId, LocalDate = request.LocalDate, Basis = request.Basis, ExpectedHours = request.ExpectedHours,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { visitId }));
    }

    [HttpGet("daycare")]
    public async Task<IActionResult> List()
    {
        var response = await Dispatcher.GetResultAsync(new GetCustomerDaycareQuery { CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("daycare/{visitId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid visitId)
    {
        var response = await Dispatcher.SendAsync(new CancelDaycareVisitCommand { VisitId = visitId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, NoContent);
    }
}

/// <summary>HO-BRD-001 section 12.4 (Phase H) -- staff daycare and the daycare price list. The branch is the active branch.</summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffDaycareController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    [HttpGet("daycare")]
    [RequireAction(Actions.Boarding.ViewStays)]
    public async Task<IActionResult> Day([FromQuery] DateOnly date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetDaycareDayQuery { BranchId = branchId, Date = date, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("daycare")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> Book([FromBody] StaffBookDaycareRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var visitId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new BookDaycareVisitCommand
        {
            VisitId = visitId, BranchId = branchId, CustomerId = request.CustomerId, CatId = request.CatId, LocalDate = request.LocalDate, Basis = request.Basis, ExpectedHours = request.ExpectedHours,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { visitId }));
    }

    [HttpPost("daycare/{visitId:guid}/check-in")]
    [RequireAction(Actions.Boarding.CheckInOut)]
    public async Task<IActionResult> CheckIn(Guid visitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CheckInDaycareVisitCommand { VisitId = visitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, NoContent);
    }

    [HttpPost("daycare/{visitId:guid}/check-out")]
    [RequireAction(Actions.Boarding.CheckInOut)]
    public async Task<IActionResult> CheckOut(Guid visitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CheckOutDaycareVisitCommand { VisitId = visitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, () => Ok(new { charge = (decimal)response.Result }));
    }

    [HttpPost("daycare/{visitId:guid}/cancel")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> Cancel(Guid visitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new CancelDaycareVisitCommand { VisitId = visitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, NoContent);
    }

    [HttpPost("daycare/{visitId:guid}/no-show")]
    [RequireAction(Actions.Boarding.Book)]
    public async Task<IActionResult> NoShow(Guid visitId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.SendAsync(new RecordDaycareNoShowCommand { VisitId = visitId, BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return Respond(response, NoContent);
    }

    [HttpGet("boarding/daycare-rate-cards")]
    [RequireAction(Actions.Boarding.ViewUnits)]
    public async Task<IActionResult> RateCards()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await Dispatcher.GetResultAsync(new GetDaycareRateCardsQuery { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("boarding/daycare-rate-cards")]
    [RequireAction(Actions.Boarding.ManageUnits)]
    public async Task<IActionResult> SetRateCard([FromBody] SetDaycareRateCardRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryParseMoney(request.HourlyRate, out var hourly) || !TryParseMoney(request.DayRate, out var day)) return BadRequest(new { error = "The rates are not numbers." });

        var rateCardId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new SetDaycareRateCardCommand
        {
            RateCardId = rateCardId, BranchId = branchId, HourlyRate = hourly, DayRate = day, TaxClassId = request.TaxClassId, EffectiveFrom = request.EffectiveFrom,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, new { rateCardId }));
    }
}
