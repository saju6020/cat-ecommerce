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

/// <summary>HO-ORD-001 §12.4 (US-ORD-06) -- delivery zones: coverage, service levels, charges, weight bands, slot windows and the cash-on-delivery
/// switch and ceiling. Administration endpoints: never maintenance-gated (HO-ADM-001 ADR-053).</summary>
[ApiController]
[Route("api/admin/delivery-zones")]
public sealed class AdminDeliveryZonesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.DeliveryZones.View)]
    public async Task<IActionResult> List()
    {
        var response = await dispatcher.GetResultAsync(new GetDeliveryZonesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.DeliveryZones.Manage)]
    public async Task<IActionResult> Create([FromBody] DeliveryZoneRequest request)
    {
        var zoneId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateDeliveryZoneCommand
        {
            ZoneId = zoneId, BranchId = request.BranchId, Name = request.Name, Priority = request.Priority, IsActive = request.IsActive,
            CodEnabled = request.CodEnabled, CodMaxOrderValue = request.CodMaxOrderValue, Coverage = request.Coverage, Services = request.Services,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { zoneId }) : OrderingResults.Failure(this, response);
    }

    /// <summary>Which active zone an address falls in -- the admin's test tool (US-ORD-06 AC2). The route constraint keeps it apart from <c>{zoneId}</c>.</summary>
    [HttpGet("resolve")]
    [RequireAction(Actions.DeliveryZones.View)]
    public async Task<IActionResult> Resolve([FromQuery] string district, [FromQuery] string? postalCode)
    {
        var response = await dispatcher.GetResultAsync(new ResolveDeliveryZoneQuery
        {
            District = district ?? string.Empty, PostalCode = postalCode, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("{zoneId:guid}")]
    [RequireAction(Actions.DeliveryZones.View)]
    public async Task<IActionResult> Get(Guid zoneId)
    {
        var response = await dispatcher.GetResultAsync(new GetDeliveryZoneQuery { ZoneId = zoneId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{zoneId:guid}")]
    [RequireAction(Actions.DeliveryZones.Manage)]
    public async Task<IActionResult> Update(Guid zoneId, [FromBody] DeliveryZoneRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateDeliveryZoneCommand
        {
            ZoneId = zoneId, BranchId = request.BranchId, Name = request.Name, Priority = request.Priority, IsActive = request.IsActive,
            CodEnabled = request.CodEnabled, CodMaxOrderValue = request.CodMaxOrderValue, Coverage = request.Coverage, Services = request.Services,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    [HttpPut("{zoneId:guid}/activation")]
    [RequireAction(Actions.DeliveryZones.Manage)]
    public async Task<IActionResult> SetActivation(Guid zoneId, [FromBody] SetActivationRequest request)
    {
        var response = await dispatcher.SendAsync(new SetDeliveryZoneActivationCommand
        {
            ZoneId = zoneId, IsActive = request.IsActive, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
