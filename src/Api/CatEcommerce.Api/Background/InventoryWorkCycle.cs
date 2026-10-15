using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-IMP-001 section 12.2 / 8.13 -- one pass of the inventory worker: (1) EXPIRY -- raise the tightest crossed threshold of every batch with stock, and
/// an <c>Expired</c> alert for a batch that has expired with stock; (2) LOW STOCK -- raise for pairs below their effective reorder point and resolve
/// the alerts of pairs that recovered. Every raise or resolve is ONE command dispatched in its OWN scope (its own <c>DbContext</c>), so one bad item is
/// logged and never stops the pass. Each pass is isolated: one that throws is logged and the next still runs (U-X3). Idempotent -- a pass that finds
/// nothing new dispatches nothing. Scoped; callable by tests.
/// </summary>
public sealed class InventoryWorkCycle(
    ILogger<InventoryWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher, InventoryWorkerState state, TimeProvider timeProvider)
{
    private static UserContext WorkerContext => new(SystemActors.InventoryWorker, Guid.Empty);

    /// <summary>One pass over every active branch -- or, for a test, over <paramref name="onlyBranchId"/> alone.</summary>
    public async Task RunAsync(CancellationToken ct = default, Guid? onlyBranchId = null)
    {
        await RunPassAsync("expiry", token => RunExpiryPassAsync(token, onlyBranchId), ct);
        await RunPassAsync("low-stock", token => RunLowStockPassAsync(token, onlyBranchId), ct);
        if (state.TryStartReconciliation(timeProvider.GetUtcNow())) await RunPassAsync("reconciliation", RunReconciliationPassAsync, ct);
    }

    /// <summary>The daily reconciliation (section 8.13): every position that differs from the entries it is derived from raises one <c>LedgerMismatch</c> alert (a live
    /// one per SKU and location -- it is never raised twice) and is logged at <c>Error</c>. The expected result is none, forever.</summary>
    public async Task RunReconciliationPassAsync(CancellationToken ct = default)
    {
        var mismatches = (await dispatcher.GetResultAsync(new GetStockReconciliationQuery { UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];

        foreach (var row in mismatches)
        {
            logger.LogError(
                "Stock ledger mismatch at location {LocationId}, batch {BatchId}: on hand {OnHand} but the ledger says {ExpectedOnHand}; reserved {Reserved} but the entries say {ExpectedReserved}.",
                row.LocationId, row.BatchId, row.OnHand, row.ExpectedOnHand, row.Reserved, row.ExpectedReserved);

            await DispatchAsync(new RaiseStockAlertCommand
            {
                AlertId = Guid.NewGuid(), Kind = "LedgerMismatch", BranchId = row.BranchId, SkuId = row.SkuId, BatchId = row.BatchId, LocationId = row.LocationId,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, $"ledger mismatch alert for batch {row.BatchId}", ct);
        }
    }

    private async Task RunPassAsync(string name, Func<CancellationToken, Task> pass, CancellationToken ct)
    {
        try { await pass(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Inventory worker {Pass} pass failed; the next pass still runs.", name); }
    }

    public async Task RunExpiryPassAsync(CancellationToken ct = default, Guid? onlyBranchId = null)
    {
        var due = await dispatcher.GetResultAsync(new GetExpiryAlertCandidatesQuery { BranchId = onlyBranchId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });

        foreach (var candidate in due.Result ?? [])
        {
            await DispatchAsync(new RaiseStockAlertCommand
            {
                AlertId = Guid.NewGuid(), Kind = candidate.IsExpired ? "Expired" : "ExpiryThreshold", BranchId = candidate.BranchId, SkuId = candidate.SkuId,
                BatchId = candidate.BatchId, ThresholdDays = candidate.ThresholdDays, DedupKey = candidate.DedupKey,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, $"expiry alert for batch {candidate.BatchId}", ct);
        }
    }

    public async Task RunLowStockPassAsync(CancellationToken ct = default, Guid? onlyBranchId = null)
    {
        var low = await dispatcher.GetResultAsync(new GetLowStockCandidatesQuery { BranchId = onlyBranchId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });
        foreach (var candidate in low.Result ?? [])
        {
            await DispatchAsync(new RaiseStockAlertCommand
            {
                AlertId = Guid.NewGuid(), Kind = "LowStock", BranchId = candidate.BranchId, SkuId = candidate.SkuId, LocationId = candidate.LocationId,
                QuantityAtRaise = candidate.Usable, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, $"low-stock alert for SKU {candidate.SkuId}", ct);
        }

        var recovered = await dispatcher.GetResultAsync(new GetRecoveredLowStockAlertsQuery { BranchId = onlyBranchId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });
        foreach (var alertId in recovered.Result ?? [])
        {
            await DispatchAsync(new ResolveStockAlertCommand
            { AlertId = alertId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, $"resolution of alert {alertId}", ct);
        }
    }

    private async Task DispatchAsync(Command command, string what, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        using var scope = scopeFactory.CreateScope();
        try
        {
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
            if (!response.IsSuccess) logger.LogWarning("Inventory worker: {What} was refused.", what);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown, not a per-item failure.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory worker failed for {What}.", what);
        }
    }
}
