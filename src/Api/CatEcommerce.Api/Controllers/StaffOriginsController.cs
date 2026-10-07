using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- sourcing origins. Reads are <c>Products.View</c>; writes are
/// <c>SourcingOrigins.Manage</c>.</summary>
[ApiController]
[Route("api/staff/shop/origins")]
public sealed class StaffOriginsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetStaffOriginsQuery
        { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Shop.SourcingOriginsManage)]
    public async Task<IActionResult> Create([FromBody] CreateSourcingOriginRequest request)
    {
        var command = new CreateSourcingOriginCommand
        {
            CountryCode = request.CountryCode, SourcingNotes = request.SourcingNotes, IsFeatured = request.IsFeatured, SortOrder = request.SortOrder,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { countryCode = command.CountryCode.ToUpperInvariant() }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{countryCode}")]
    [RequireAction(Actions.Shop.SourcingOriginsManage)]
    public async Task<IActionResult> Update(string countryCode, [FromBody] SourcingOriginRequest request)
    {
        var command = new UpdateSourcingOriginCommand
        {
            CountryCode = countryCode, SourcingNotes = request.SourcingNotes, IsFeatured = request.IsFeatured, SortOrder = request.SortOrder,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        if (response.IsSuccess) return NoContent();

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.OriginNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }
}
