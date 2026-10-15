using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ORD-001 §12.4 (US-ORD-11) -- couriers. There is no courier API (SRS Q-4): a courier is master data a dispatch names, with an optional
/// tracking-link template. <c>Couriers.Manage</c> only; the dispatch screen's read-only list is a staff route (Phase F).</summary>
[ApiController]
[Route("api/admin/couriers")]
public sealed class AdminCouriersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Couriers.Manage)]
    public async Task<IActionResult> List()
    {
        var response = await dispatcher.GetResultAsync(new GetCouriersQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Couriers.Manage)]
    public async Task<IActionResult> Create([FromBody] CourierRequest request)
    {
        var courierId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateCourierCommand
        {
            CourierId = courierId, Name = request.Name, TrackingUrlTemplate = request.TrackingUrlTemplate, SupportsCod = request.SupportsCod, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { courierId }) : OrderingResults.Failure(this, response);
    }

    [HttpPut("{courierId:guid}")]
    [RequireAction(Actions.Couriers.Manage)]
    public async Task<IActionResult> Update(Guid courierId, [FromBody] CourierRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateCourierCommand
        {
            CourierId = courierId, Name = request.Name, TrackingUrlTemplate = request.TrackingUrlTemplate, SupportsCod = request.SupportsCod, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
