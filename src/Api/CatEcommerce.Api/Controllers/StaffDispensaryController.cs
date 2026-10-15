using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 section 12.4 (Phase F) -- the two reads a vet needs to record a treatment: the active branch's clinic dispensary items and, for one
/// item, the batches Inventory would let them use (FEFO order, the default marked). <c>Visits.WriteNote</c> (Vet): the same access as the
/// treatment endpoint they serve. Branch from <c>X-Branch-Id</c>; these are stock reads, not clinical-record reads, so they carry no
/// <c>[ClinicalRecordAccess]</c> (they name no cat and no visit).
/// </summary>
[ApiController]
[Route("api/staff/dispensary")]
public sealed class StaffDispensaryController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet("items")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> GetItems([FromQuery] string? search = null, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetDispensaryItemsQuery
        {
            BranchId = branchId, Search = search, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpGet("items/{skuId:guid}/batches")]
    [RequireAction(Actions.Visits.WriteNote)]
    public async Task<IActionResult> GetBatches(Guid skuId)
    {
        if (!ClinicalResponses.TryResolveBranch(this, activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetIssuableBatchesQuery
        {
            BranchId = branchId, SkuId = skuId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }
}
