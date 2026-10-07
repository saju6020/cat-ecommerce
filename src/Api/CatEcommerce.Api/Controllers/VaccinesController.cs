using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Vaccination;
using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Read.Queries.Vaccination;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-CAT-001 section 12.4. Attributes added by HO-ADM-001 §12.3.</summary>
[ApiController]
[Route("api/vaccines")]
public sealed class VaccinesController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost]
    [RequireAction(Actions.Vaccines.Manage)]
    public async Task<IActionResult> RegisterVaccine([FromBody] RegisterVaccineRequest request)
    {
        var command = new RegisterVaccineCommand
        {
            VaccineId = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            IsCore = request.IsCore,
            ValidityMonths = request.ValidityMonths,
            Description = request.Description,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetVaccines), null, new { vaccineId = response.Result })
            : BadRequest(response.ValidationResult);
    }

    [HttpGet]
    [RequireAction(Actions.Vaccines.View)]
    public async Task<IActionResult> GetVaccines(
        [FromQuery] bool activeOnly = false, [FromQuery] bool coreOnly = false,
        [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetVaccinesQuery
        {
            ActiveOnly = activeOnly,
            CoreOnly = coreOnly,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };

        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }
}
