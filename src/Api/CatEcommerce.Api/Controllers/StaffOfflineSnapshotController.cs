using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-XCT-001 section 12.4 (US-XCT-01, NFR-SEC-8). The offline front-desk snapshot of the active branch. Passive (never extends the idle window),
/// <c>no-store, private</c>, strong ETag; <c>If-None-Match</c> equal answers <c>304</c> with no audit row, otherwise the issue is audited (C10) and
/// the body returned.
/// </summary>
[ApiController]
public sealed class StaffOfflineSnapshotController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranch) : ControllerBase
{
    [HttpGet("api/staff/offline-snapshot")]
    [RequireAction(Actions.Offline.FrontDeskSnapshot)]
    [PassiveSession]
    public async Task<IActionResult> Get()
    {
        Response.Headers.CacheControl = "no-store, private";

        var active = activeBranch.GetActiveBranchId();
        if (active.Refusal is { } refusal) return StatusCode(403, new { error = refusal.ToString() });
        if (active.BranchId is not { } branchId)
            return BadRequest(new { errors = new[] { new { errorCode = BusinessViolationCodes.ActiveBranchRequired, errorMessage = OperationsMessages.ActiveBranchRequired } } });

        var user = userContextProvider.GetUserContext();
        var snapshot = (await dispatcher.GetResultAsync(new GetFrontDeskOfflineSnapshotQuery
        {
            BranchId = branchId, CallerRoles = user.Roles ?? [], StaffAccountId = user.UserId, UserContext = user, CorrelationId = Guid.NewGuid()
        })).Result;
        if (snapshot is null) return NotFound();

        Response.Headers.ETag = snapshot.ETag;
        if (Request.Headers.IfNoneMatch.Any(value => string.Equals(value?.Trim(), snapshot.ETag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);

        var issued = await dispatcher.SendAsync(new RecordOfflineSnapshotIssuedCommand
        {
            BranchId = branchId, LocalDate = snapshot.LocalDate, SectionCounts = snapshot.SectionCounts, ETag = snapshot.ETag,
            UserContext = user, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return issued.IsSuccess ? Ok(snapshot.Body) : BadRequest(issued.ValidationResult);
    }
}
