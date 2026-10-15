using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Operations;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-XCT-001 section 12.4 (US-XCT-05). Backup health, the quarterly restore test and its record.</summary>
[ApiController]
[Route("api/admin/operations")]
public sealed class AdminOperationsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    /// <summary>Q7 -- the latest backup log row per kind, staleness flags, and when the next restore test is due.</summary>
    [HttpGet("backup-status")]
    [RequireAction(Actions.Operations.View)]
    public async Task<IActionResult> GetBackupStatus()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok((await dispatcher.GetResultAsync(new GetBackupStatusQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() })).Result);
    }

    [HttpGet("restore-tests")]
    [RequireAction(Actions.Operations.View)]
    public async Task<IActionResult> GetRestoreTests([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetRestoreTestsQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalidPaging) return invalidPaging;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("restore-tests/{id:guid}")]
    [RequireAction(Actions.Operations.View)]
    public async Task<IActionResult> GetRestoreTest(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetRestoreTestQuery
        {
            RestoreTestId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>C12 -- records a drill. The outcome, RPO and RTO in the answer are the system's own computation.</summary>
    [HttpPost("restore-tests")]
    [RequireAction(Actions.Operations.RecordRestoreTest)]
    public async Task<IActionResult> RecordRestoreTest([FromBody] RecordRestoreTestRequest request)
    {
        var response = await dispatcher.SendAsync(new RecordRestoreTestCommand
        {
            RestoreTestId = Guid.NewGuid(),
            PerformedOn = request.PerformedOn,
            Environment = request.Environment ?? string.Empty,
            BackupSetLabel = request.BackupSetLabel ?? string.Empty,
            SimulatedFailureAt = request.SimulatedFailureAt,
            RecoveredTo = request.RecoveredTo,
            RestoreStartedAt = request.RestoreStartedAt,
            RestoreCompletedAt = request.RestoreCompletedAt,
            Checks = (request.Checks ?? []).Select(c => new RestoreCheckInput(c.Name ?? string.Empty, c.Passed, c.Detail)).ToList(),
            DrillOutput = request.DrillOutput ?? string.Empty,
            Notes = request.Notes,
            SupersedesId = request.SupersedesId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, response.Result) : BadRequest(response.ValidationResult);
    }
}
