using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Filters;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- the portal's clinical reads (FR-POR-3): the owner reads their cat's <b>finalised</b> visits, the discharge
/// summary (JSON and PDF) and lab results. Everything is <c>Visits.ViewOwn</c> and every call is access-logged with the
/// customer as the actor. A visit that is not the caller's <i>current</i> cat's, or is not finalised, is a <c>404</c> --
/// after an ownership transfer the previous owner reads nothing, and no response hints that the record exists.
/// The cat routes are ownership-scoped by <c>OwnerScope.Cat</c>; the visit routes carry no cat id, so the query itself
/// resolves the visit's cat and its current owner.
/// </summary>
[ApiController]
[Route("api/customers/me")]
public sealed class CustomerVisitsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IDischargeSummaryPdfRenderer pdfRenderer) : ControllerBase
{
    [HttpGet("cats/{catId:guid}/visits")]
    [RequireAction(Actions.Visits.ViewOwn, OwnerScope.Cat)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.VisitList))]
    public async Task<IActionResult> GetVisits(Guid catId)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerCatVisitsQuery { CatId = catId, CustomerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("visits/{visitId:guid}")]
    [RequireAction(Actions.Visits.ViewOwn, OwnerScope.CatCollection)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DischargeSummary))]
    public async Task<IActionResult> GetSummary(Guid visitId)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerVisitSummaryQuery { VisitId = visitId, CustomerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("visits/{visitId:guid}/discharge-summary.pdf")]
    [RequireAction(Actions.Visits.ViewOwn, OwnerScope.CatCollection)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DischargeSummary))]
    public async Task<IActionResult> GetSummaryPdf(Guid visitId)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerVisitSummaryQuery { VisitId = visitId, CustomerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : ClinicalResponses.Pdf(this, pdfRenderer, response.Result);
    }

    [HttpGet("cats/{catId:guid}/lab-results")]
    [RequireAction(Actions.Visits.ViewOwn, OwnerScope.Cat)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.DiagnosticOrder))]
    public async Task<IActionResult> GetLabResults(Guid catId)
    {
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerLabResultsQuery { CatId = catId, CustomerId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
