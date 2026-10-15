using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Surgery;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-SRG-001 section 12.4 (US-SRG-11, ADR-171) -- community-cat campaign price lists. Reading needs <c>Surgery.View</c>; writing <c>Surgery.ManageCampaigns</c> (Administrator). The
/// case list is the active branch's only. A campaign is deactivated, never deleted.
/// </summary>
[ApiController]
[Route("api/staff/surgery-campaigns")]
public sealed class StaffSurgeryCampaignsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : SurgeryControllerBase(activeBranchAccessor)
{
    private UserContext User => userContextProvider.GetUserContext();

    [HttpGet]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetSurgeryCampaignsQuery { ActiveOnly = activeOnly, UserContext = User, CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("applicable")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Applicable([FromQuery] Guid serviceId, [FromQuery] DateOnly date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetApplicableCampaignsQuery { BranchId = branchId, ServiceId = serviceId, Date = date, UserContext = User, CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}/cases")]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> Cases(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var query = new GetCampaignCasesQuery { CampaignId = id, BranchId = branchId, From = from, To = to, UserContext = User, CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Surgery.ManageCampaigns)]
    public async Task<IActionResult> Create([FromBody] CreateSurgeryCampaignRequest request)
    {
        var response = await dispatcher.SendAsync(new CreateSurgeryCampaignCommand
        {
            Code = request.Code, Name = request.Name, Description = request.Description ?? "", ValidFrom = request.ValidFrom, ValidTo = request.ValidTo,
            BranchIds = request.BranchIds ?? [], Prices = request.Prices.Select(p => new SurgeryCampaignPriceInput(p.ServiceId, p.Amount)).ToList(), MaxCases = request.MaxCases,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        return response.IsSuccess ? StatusCode(201, new { campaignId = (Guid)response.Result }) : RespondToFailure(response);
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Surgery.ManageCampaigns)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSurgeryCampaignRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateSurgeryCampaignCommand
        {
            CampaignId = id, ExpectedVersion = request.ExpectedVersion, Name = request.Name, Description = request.Description ?? "", ValidFrom = request.ValidFrom, ValidTo = request.ValidTo,
            BranchIds = request.BranchIds ?? [], Prices = request.Prices.Select(p => new SurgeryCampaignPriceInput(p.ServiceId, p.Amount)).ToList(), MaxCases = request.MaxCases,
            UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryCampaignNotFound);
    }

    [HttpPost("{id:guid}/active")]
    [RequireAction(Actions.Surgery.ManageCampaigns)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetSurgeryCampaignActiveRequest request)
    {
        var response = await dispatcher.SendAsync(new SetSurgeryCampaignActiveCommand
        { CampaignId = id, IsActive = request.IsActive, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.SurgeryCampaignNotFound);
    }
}
