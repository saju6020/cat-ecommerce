using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-PHM-001 §12.4 -- the formulary reads staff use to prescribe and dispense (<c>Formulary.View</c>): the list, the detail and the picker.</summary>
[ApiController]
[Route("api/staff/formulary")]
public sealed class FormularyController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet]
    [RequireAction(Actions.Formulary.View)]
    public async Task<IActionResult> GetMedicines(
        [FromQuery] string? search, [FromQuery] bool? isControlled, [FromQuery] bool? isActive, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetFormularyMedicinesQuery
        {
            Search = search, IsControlled = isControlled, IsActive = isActive, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } refusal) return refusal;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{medicineId:guid}")]
    [RequireAction(Actions.Formulary.View)]
    public async Task<IActionResult> GetMedicine(Guid medicineId)
    {
        var response = await dispatcher.GetResultAsync(new GetFormularyMedicineQuery
        { MedicineId = medicineId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
