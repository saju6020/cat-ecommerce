using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- the clinic service catalogue and its prices.</summary>
[ApiController]
[Route("api/admin/services")]
public sealed class AdminServicesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Services.View)]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetAdminServicesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{serviceId:guid}")]
    [RequireAction(Actions.Services.View)]
    public async Task<IActionResult> GetOne(Guid serviceId)
    {
        var response = await dispatcher.GetResultAsync(new GetAdminServiceQuery { ServiceId = serviceId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Services.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateClinicServiceRequest request)
    {
        var serviceId = Guid.NewGuid();
        var command = new CreateClinicServiceCommand
        {
            ServiceId = serviceId, Code = request.Code, Name = request.Name, Description = request.Description,
            Category = request.Category, DurationMinutes = request.DurationMinutes, QualifiedRoleIds = request.QualifiedRoleIds,
            RequiredRoomKind = request.RequiredRoomKind, TaxClassId = request.TaxClassId, IsBookableOnline = request.IsBookableOnline,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { serviceId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{serviceId:guid}")]
    [RequireAction(Actions.Services.Manage)]
    public async Task<IActionResult> Update(Guid serviceId, [FromBody] UpdateClinicServiceRequest request)
    {
        var command = new UpdateClinicServiceCommand
        {
            ServiceId = serviceId, Name = request.Name, Description = request.Description, Category = request.Category,
            DurationMinutes = request.DurationMinutes, QualifiedRoleIds = request.QualifiedRoleIds,
            RequiredRoomKind = request.RequiredRoomKind, TaxClassId = request.TaxClassId, IsBookableOnline = request.IsBookableOnline,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPut("{serviceId:guid}/activation")]
    [RequireAction(Actions.Services.Manage)]
    public async Task<IActionResult> SetActivation(Guid serviceId, [FromBody] SetServiceActivationRequest request)
    {
        var command = new SetClinicServiceActivationCommand
        {
            ServiceId = serviceId, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("{serviceId:guid}/prices")]
    [RequireAction(Actions.Services.View)]
    public async Task<IActionResult> GetPrices(Guid serviceId)
    {
        var response = await dispatcher.GetResultAsync(new GetServicePricesQuery { ServiceId = serviceId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        TruncationHeader.ApplyIfAtCap(Response, response.Result.Count);
        return Ok(response.Result);
    }

    [HttpPost("{serviceId:guid}/prices")]
    [RequireAction(Actions.Services.Manage)]
    public async Task<IActionResult> SetPrice(Guid serviceId, [FromBody] SetServicePriceRequest request)
    {
        if (!decimal.TryParse(request.Amount, out var amount)) return BadRequest(new { error = "Invalid amount." });

        var priceId = Guid.NewGuid();
        var command = new SetServicePriceCommand
        {
            PriceId = priceId, ServiceId = serviceId, Amount = amount, CurrencyCode = request.CurrencyCode, EffectiveFrom = request.EffectiveFrom,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { priceId }) : BadRequest(response.ValidationResult);
    }
}
