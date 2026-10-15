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

/// <summary>HO-IMP-001 §12.4 (US-IMP-14, ADR-108) -- cycle counts and full stock takes. Counting is BLIND (the sheet never shows book quantities while the
/// count is Open); submitting freezes the variance report and writes NOTHING to the ledger; committing posts exactly the reported variances, or waits for
/// an approver when they are worth more than the threshold.</summary>
[ApiController]
[Route("api/staff/inventory/counts")]
public sealed class StaffCountsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? locationId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetStockCountsQuery { BranchId = branchId, Status = status, LocationId = locationId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Stock.Count)]
    public async Task<IActionResult> Open([FromBody] OpenStockCountRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var countId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new OpenStockCountCommand
        {
            CountId = countId, BranchId = branchId, LocationId = request.LocationId, Scope = request.Scope, SkuIds = request.SkuIds,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { countId, number = response.Result as string });
    }

    /// <summary>The count sheet -- BLIND while the count is Open: no book quantity, no variance on any line.</summary>
    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockCountSheetQuery
        { CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{id:guid}/lines")]
    [RequireAction(Actions.Stock.Count)]
    public async Task<IActionResult> RecordLines(Guid id, [FromBody] RecordCountLinesRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordCountLinesCommand
        {
            CountId = id, BranchId = branchId, Lines = request.Lines,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/submit")]
    [RequireAction(Actions.Stock.Count)]
    public async Task<IActionResult> Submit(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SubmitStockCountCommand
        {
            CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    /// <summary>US-IMP-14 AC1 -- the variance report, available once the count is submitted; nothing is in the ledger yet.</summary>
    [HttpGet("{id:guid}/variance")]
    [RequireAction(Actions.Stock.View)]
    public async Task<IActionResult> Variance(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStockCountVarianceQuery
        { CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{id:guid}/commit")]
    [RequireAction(Actions.Stock.Count)]
    public async Task<IActionResult> Commit(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CommitStockCountCommand
        {
            CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? Ok(new { status = ((CommittedStockCount)response.Result!).Status }) : this.Failure(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Stock.Count)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelStockCountCommand
        {
            CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/approve")]
    [RequireAction(Actions.Stock.ApproveAdjustment)]
    public async Task<IActionResult> Approve(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApproveStockCountCommand
        {
            CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/reject")]
    [RequireAction(Actions.Stock.ApproveAdjustment)]
    public async Task<IActionResult> Reject(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RejectStockCountCommand
        {
            CountId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
