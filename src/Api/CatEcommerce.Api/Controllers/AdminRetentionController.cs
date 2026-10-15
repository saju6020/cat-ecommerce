using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-XCT-001 section 12.4 -- the retention and erasure reads (and, in Phase C, the schedule, runs and review).</summary>
[ApiController]
[Route("api/admin/retention")]
public sealed class AdminRetentionController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    /// <summary>Q3 -- every record class with its policy, effective period (from settings), action at the end and the classified tables.</summary>
    [HttpGet("schedule")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetSchedule() =>
        Ok((await dispatcher.GetResultAsync(new GetRetentionScheduleQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() })).Result);

    /// <summary>Q4 -- retention runs, newest first.</summary>
    [HttpGet("runs")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetRuns([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetRetentionRunsQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalidPaging) return invalidPaging;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    /// <summary>Q4 -- one run with every pass: counts and codes only.</summary>
    [HttpGet("runs/{runId:guid}")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetRun(Guid runId)
    {
        var response = await dispatcher.GetResultAsync(new GetRetentionRunQuery
        {
            RetentionRunId = runId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>
    /// C5 -- asks for a manual run. The worker picks it up on its next poll, so this answers <c>201</c> with the run id and <c>Requested</c> (the run
    /// itself is observable through <c>GET runs/{id}</c>). <c>409</c> while another run is waiting or running.
    /// </summary>
    [HttpPost("runs")]
    [RequireAction(Actions.Retention.Run)]
    public async Task<IActionResult> RequestRun()
    {
        var retentionRunId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RequestRetentionRunCommand
        {
            RetentionRunId = retentionRunId, Trigger = RetentionRunTrigger.Manual.DisplayName,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess) return StatusCode(201, new { retentionRunId, status = RetentionRunStatus.Requested.DisplayName });

        var inProgress = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.RetentionRunAlreadyInProgress) == true;
        return inProgress ? Conflict(response.ValidationResult) : BadRequest(response.ValidationResult);
    }

    /// <summary>Q5 -- records past their statutory period, for review. Nothing is deleted automatically.</summary>
    [HttpGet("review")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetReview() =>
        Ok((await dispatcher.GetResultAsync(new GetRetentionReviewQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() })).Result);

    /// <summary>Q6 -- the erasure ledger list. <c>status</c> is Open or Completed.</summary>
    [HttpGet("erasure-requests")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetErasureRequests([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!EnumQueryParameter.TryParse<ErasureRequestStatus>(status, out var parsedStatus))
            return BadRequest(new { error = $"'{status}' is not a valid erasure request status." });

        var query = new GetErasureRequestsQuery
        {
            Status = parsedStatus?.DisplayName, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalidPaging) return invalidPaging;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    /// <summary>Q6 -- one ledger: scopes with status, held-until date, basis, attempts and error code. No name, phone or email (they no longer exist).</summary>
    [HttpGet("erasure-requests/{customerAccountId:guid}")]
    [RequireAction(Actions.Retention.View)]
    public async Task<IActionResult> GetErasureRequest(Guid customerAccountId)
    {
        var response = await dispatcher.GetResultAsync(new GetErasureRequestQuery
        {
            CustomerAccountId = customerAccountId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
