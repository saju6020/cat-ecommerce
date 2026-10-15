using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Boarding;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-BRD-001 section 12.4 -- the owner's side. Every handler resolves the stay by id AND caller; someone else's stay is <c>404</c> (section 12.3).</summary>
[ApiController]
[Route("api/customers/me/boarding")]
[RequireCustomerSession]
public sealed class CustomerBoardingController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : BoardingControllerBase(dispatcher, userContextProvider, activeBranchAccessor)
{
    private Guid CustomerId => Caller.UserId;

    [HttpPost("enquiries")]
    [EnableRateLimiting(BoardingRateLimitPolicies.Booking)]
    public async Task<IActionResult> CreateEnquiry([FromBody] CreateBoardingEnquiryRequest request)
    {
        var stayId = Guid.NewGuid();
        var response = await Dispatcher.SendAsync(new CreateBoardingEnquiryCommand
        {
            StayId = stayId, BranchId = request.BranchId, CustomerId = CustomerId, IsStaff = false, ArrivalOn = request.ArrivalOn, DepartureOn = request.DepartureOn,
            UnitType = request.UnitType, CatIds = request.CatIds, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => StatusCode(201, EnquiryBody((EnquiryResult)response.Result)));
    }

    [HttpPut("stays/{stayId:guid}/enquiry")]
    [EnableRateLimiting(BoardingRateLimitPolicies.Booking)]
    public async Task<IActionResult> ChangeEnquiry(Guid stayId, [FromBody] ChangeBoardingEnquiryRequest request)
    {
        var response = await Dispatcher.SendAsync(new ChangeBoardingEnquiryCommand
        {
            StayId = stayId, CustomerId = CustomerId, ArrivalOn = request.ArrivalOn, DepartureOn = request.DepartureOn, UnitType = request.UnitType, CatIds = request.CatIds,
            UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return Respond(response, () => Ok(EnquiryBody((EnquiryResult)response.Result)));
    }

    [HttpPut("stays/{stayId:guid}/cats/{catId:guid}/care-profile")]
    public async Task<IActionResult> SetCareProfile(Guid stayId, Guid catId, [FromBody] CareProfileRequest request)
    {
        if (!TryMapCareProfile(request, out var input, out var error)) return BadRequest(new { error });

        var response = await Dispatcher.SendAsync(new SetStayCareProfileCommand
        { StayId = stayId, CatId = catId, CustomerId = CustomerId, Profile = input!, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpPost("stays/{stayId:guid}/reserve")]
    [EnableRateLimiting(BoardingRateLimitPolicies.Booking)]
    [BoardingClaimEndpoint]
    public async Task<IActionResult> Reserve(Guid stayId)
    {
        var response = await Dispatcher.SendAsync(new ReserveBoardingStayCommand
        { StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (ReserveResult)response.Result;
        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
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

    [HttpPost("stays/{stayId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid stayId, [FromBody] CancelBoardingStayRequest request)
    {
        var response = await Dispatcher.SendAsync(new CancelBoardingStayCommand
        { StayId = stayId, CustomerId = CustomerId, Note = request.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, () => Ok(new { depositForfeited = ((CancelResult)response.Result).DepositForfeited }));
    }

    [HttpGet("stays")]
    public async Task<IActionResult> GetStays([FromQuery] string? scope, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetCustomerStaysQuery { CustomerId = CustomerId, Scope = string.IsNullOrWhiteSpace(scope) ? "upcoming" : scope, UserContext = Caller, CorrelationId = Guid.NewGuid() };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = Math.Min(pageSize.Value, 100);

        var response = await Dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("stays/{stayId:guid}/diary")]
    public async Task<IActionResult> GetDiary(Guid stayId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var response = await Dispatcher.GetResultAsync(new GetStayDiaryQuery { StayId = stayId, CustomerId = CustomerId, From = from, To = to, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("stays/{stayId:guid}/incidents/{incidentId:guid}")]
    public async Task<IActionResult> GetIncident(Guid stayId, Guid incidentId)
    {
        var response = await Dispatcher.GetResultAsync(new GetCustomerIncidentQuery { StayId = stayId, IncidentId = incidentId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("stays/{stayId:guid}/incidents/{incidentId:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeIncident(Guid stayId, Guid incidentId)
    {
        var response = await Dispatcher.SendAsync(new AcknowledgeIncidentCommand
        { IncidentId = incidentId, StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("stays/{stayId:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid stayId)
    {
        var response = await Dispatcher.GetResultAsync(new GetStaySummaryQuery { StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("stays/{stayId:guid}/approvals/{approvalId:guid}")]
    public async Task<IActionResult> GetApproval(Guid stayId, Guid approvalId)
    {
        var detail = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        var approval = detail.Result?.Approvals.FirstOrDefault(a => a.Id == approvalId);
        return approval is null ? NotFound() : Ok(approval);
    }

    [HttpPost("stays/{stayId:guid}/approvals/{approvalId:guid}/respond")]
    public async Task<IActionResult> RespondToApproval(Guid stayId, Guid approvalId, [FromBody] VetApprovalAnswerRequest request)
    {
        var response = await Dispatcher.SendAsync(new RespondToVetChargeApprovalCommand
        { StayId = stayId, ApprovalId = approvalId, CustomerId = CustomerId, Approve = request.Approve, Note = request.Note, UserContext = Caller, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return Respond(response, NoContent);
    }

    [HttpGet("stays/{stayId:guid}")]
    public async Task<IActionResult> GetStay(Guid stayId)
    {
        var response = await Dispatcher.GetResultAsync(new GetStayDetailQuery { StayId = stayId, CustomerId = CustomerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
