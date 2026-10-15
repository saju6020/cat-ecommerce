using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Prescription;
using CatEcommerce.Api.Filters;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Domain.DomainServices;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-PHM-001 §12.4 (US-PHM-08, ADR-149, BR-6) -- staff diet authorisations. Authorising, extending and revoking are <c>DietAuthorisations.Issue</c>: Vet alone -- the
/// filter refuses anyone else with a <c>403</c> and an <c>AuthorizationDenied</c> audit row before an action runs (the handler checks again). Reads are logged as clinical
/// record access (NFR-SEC-8).
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffDietAuthorisationsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("diet-products")]
    [RequireAction(Actions.DietAuthorisations.Issue)]
    public async Task<IActionResult> SearchProducts([FromQuery] string? search)
    {
        var response = await dispatcher.GetResultAsync(new SearchDietProductsQuery
        { Search = search, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("cats/{catId:guid}/diet-authorisations")]
    [RequireAction(Actions.DietAuthorisations.Issue)]
    public async Task<IActionResult> Authorise(Guid catId, [FromBody] AuthoriseDietRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var authorisationId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AuthoriseDietCommand
        {
            AuthorisationId = authorisationId, CatId = catId, VisitId = request.VisitId, ProductId = request.ProductId, ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo, Notes = request.Notes, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { authorisationId }) : RespondToFailure(response, BusinessViolationCodes.CatNotFound);
    }

    [HttpGet("cats/{catId:guid}/diet-authorisations")]
    [RequireAction(Actions.DietAuthorisations.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.ClinicalHistory))]
    public async Task<IActionResult> GetForCat(Guid catId, [FromQuery] string? scope)
    {
        if (scope is not null && scope is not ("active" or "all")) return BadRequest(new { error = "scope must be 'active' or 'all'." });

        var response = await dispatcher.GetResultAsync(new GetCatDietAuthorisationsQuery
        { CatId = catId, Scope = scope ?? "active", UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("diet-authorisations/{authorisationId:guid}/extend")]
    [RequireAction(Actions.DietAuthorisations.Issue)]
    public async Task<IActionResult> Extend(Guid authorisationId, [FromBody] ExtendDietAuthorisationRequest request)
    {
        var response = await dispatcher.SendAsync(new ExtendDietAuthorisationCommand
        {
            AuthorisationId = authorisationId, ValidTo = request.ValidTo, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.DietAuthorisationNotFound);
    }

    [HttpPost("diet-authorisations/{authorisationId:guid}/revoke")]
    [RequireAction(Actions.DietAuthorisations.Issue)]
    public async Task<IActionResult> Revoke(Guid authorisationId, [FromBody] RevokeDietAuthorisationRequest request)
    {
        var response = await dispatcher.SendAsync(new RevokeDietAuthorisationCommand
        {
            AuthorisationId = authorisationId, Reason = request.Reason, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.DietAuthorisationNotFound);
    }
}
