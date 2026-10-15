using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Filters;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- one cat's clinical history for staff (US-CLN-13, FR-CLN-14): a summary call plus paged sections.
/// <c>Visits.View</c> (Nurse, Vet, Administrator); every call is written to the clinical-record access log before it
/// runs (§8.11) -- the cat id in the route is what the log row names. The history is the cat's, so it spans branches.
/// Reading it touches nothing: a vet's open draft is unaffected (US-CLN-13 AC2 "without losing my draft").
/// </summary>
[ApiController]
[Route("api/staff/cats/{catId:guid}/clinical-history")]
public sealed class StaffClinicalHistoryController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.ClinicalHistory))]
    public async Task<IActionResult> GetSummary(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetClinicalHistorySummaryQuery
        {
            CatId = catId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("{section}")]
    [RequireAction(Actions.Visits.View)]
    [ClinicalRecordAccess(nameof(ClinicalRecordKind.ClinicalHistory))]
    public async Task<IActionResult> GetSection(Guid catId, string section, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetClinicalHistorySectionQuery
        {
            CatId = catId, Section = section, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
