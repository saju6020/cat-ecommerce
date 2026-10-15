using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 section 12.4 (US-IMP-16 AC2) -- storage-condition breaches of the ACTIVE branch. Anyone who handles stock may REPORT one; moving the stock to
/// quarantine and closing the report are the inventory manager's.</summary>
[ApiController]
[Route("api/staff/inventory/storage-breaches")]
public sealed class StaffStorageBreachesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStorageBreachesQuery { BranchId = branchId, Status = status, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpPost]
    [RequireAction(Actions.StorageBreaches.Report)]
    public async Task<IActionResult> Report([FromBody] ReportStorageBreachRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var breachId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new ReportStorageBreachCommand
        {
            BreachId = breachId, BranchId = branchId, LocationId = request.LocationId, ObservedOn = (request.ObservedOn ?? timeProvider.GetUtcNow()).ToUniversalTime(), Condition = request.Condition ?? string.Empty,
            TemperatureC = request.TemperatureC, DurationMinutes = request.DurationMinutes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess
            ? StatusCode(201, new { breachId, affectedBatchCount = ((ReportedStorageBreach)response.Result!).AffectedBatchCount })
            : this.Failure(response);
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStorageBreachQuery
        { BreachId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{id:guid}/quarantine")]
    [RequireAction(Actions.StorageBreaches.Manage)]
    public async Task<IActionResult> Quarantine(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        return this.NoContentOrFailure(await dispatcher.SendAsync(new QuarantineStorageBreachStockCommand
        { BreachId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true }));
    }

    [HttpPost("{id:guid}/close")]
    [RequireAction(Actions.StorageBreaches.Manage)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseStorageBreachRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        return this.NoContentOrFailure(await dispatcher.SendAsync(new CloseStorageBreachCommand
        { BreachId = id, BranchId = branchId, Note = request.Note ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true }));
    }
}
