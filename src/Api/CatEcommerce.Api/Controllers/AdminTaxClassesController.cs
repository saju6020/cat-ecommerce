using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- tax classes and their effective-dated rates.</summary>
[ApiController]
[Route("api/admin/tax-classes")]
public sealed class AdminTaxClassesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.TaxClasses.View)]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetTaxClassesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.TaxClasses.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateTaxClassRequest request)
    {
        var taxClassId = Guid.NewGuid();
        var command = new CreateTaxClassCommand
        {
            TaxClassId = taxClassId, Code = request.Code, Name = request.Name,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { taxClassId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{taxClassId:guid}")]
    [RequireAction(Actions.TaxClasses.Manage)]
    public async Task<IActionResult> Update(Guid taxClassId, [FromBody] UpdateTaxClassRequest request)
    {
        var command = new UpdateTaxClassCommand
        {
            TaxClassId = taxClassId, Name = request.Name, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpGet("{taxClassId:guid}/rates")]
    [RequireAction(Actions.TaxClasses.View)]
    public async Task<IActionResult> GetRates(Guid taxClassId)
    {
        var response = await dispatcher.GetResultAsync(new GetTaxRatesQuery { TaxClassId = taxClassId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        TruncationHeader.ApplyIfAtCap(Response, response.Result.Count);
        return Ok(response.Result);
    }

    [HttpPost("{taxClassId:guid}/rates")]
    [RequireAction(Actions.TaxClasses.Manage)]
    public async Task<IActionResult> SetRate(Guid taxClassId, [FromBody] SetTaxRateRequest request)
    {
        var taxRateId = Guid.NewGuid();
        var command = new SetTaxRateCommand
        {
            TaxRateId = taxRateId, TaxClassId = taxClassId, RatePercent = request.RatePercent, EffectiveFrom = request.EffectiveFrom,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { taxRateId }) : BadRequest(response.ValidationResult);
    }
}
