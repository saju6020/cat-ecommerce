using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-ORD-001 §12.2, BR-16, FR-ORD-22 -- one pass of the order worker, three independent sub-passes: (1) <b>expire</b> unpaid bank-transfer orders past their
/// window, one <see cref="ExpireUnpaidOrderCommand"/> per order in its own scope (so one bad order is logged and never stops the rest, and a confirmation that
/// races an expiry wins -- the command is a no-op for an order that is no longer expirable); (2) <b>abandon</b> stale guest carts, one batch of at most 200
/// per command until a batch comes back short; (3) <b>purge</b> idempotency records older than 24 hours. A sub-pass that throws is logged and the next still
/// runs (U-X1). Idempotent; Scoped; callable by tests.
/// </summary>
public sealed class OrderWorkCycle(
    ILogger<OrderWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher, IIdempotencyStore idempotency, TimeProvider timeProvider)
{
    private static UserContext WorkerContext => new(SystemActors.OrderWorker, Guid.Empty) { UserName = "order-worker" };

    /// <summary>One pass. <paramref name="onlyBranchId"/> (tests only) runs the expiry pass alone, for one branch, so a test that moves its clock forward never expires another test's orders or purges another test's idempotency records in the shared database.</summary>
    public async Task RunAsync(CancellationToken ct = default, Guid? onlyBranchId = null, bool withIdempotencyPurge = false)
    {
        await RunPassAsync("expiry", token => ExpireUnpaidOrdersAsync(token, onlyBranchId), ct);
        if (onlyBranchId is not null) // a test restricted to one branch runs the expiry pass alone (and, only when it tests the purge's failure path, that pass too)
        {
            if (withIdempotencyPurge) await RunPassAsync("idempotency", PurgeIdempotencyRecordsAsync, ct);
            return;
        }

        await RunPassAsync("guest-carts", PurgeGuestCartsAsync, ct);
        await RunPassAsync("idempotency", PurgeIdempotencyRecordsAsync, ct);
    }

    public async Task ExpireUnpaidOrdersAsync(CancellationToken ct = default, Guid? onlyBranchId = null)
    {
        var due = await dispatcher.GetResultAsync(new GetExpirableOrdersQuery { BranchId = onlyBranchId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });

        foreach (var orderId in due.Result ?? [])
        {
            await DispatchAsync(
                new ExpireUnpaidOrderCommand { OrderId = orderId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true },
                $"expiry of order {orderId}", ct);
        }
    }

    public async Task PurgeGuestCartsAsync(CancellationToken ct = default)
    {
        for (var batch = 0; batch < 50 && !ct.IsCancellationRequested; batch++)
        {
            using var scope = scopeFactory.CreateScope();
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(
                new PurgeStaleGuestCartsCommand { UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

            if (!response.IsSuccess || response.Result is not PurgeStaleGuestCartsResult { Abandoned: >= CartRules.PurgeBatchSize }) return;
        }
    }

    public async Task PurgeIdempotencyRecordsAsync(CancellationToken ct = default)
    {
        var olderThan = timeProvider.GetUtcNow().AddHours(-IdempotencyRules.RetentionHours);
        int purged;
        do
        {
            purged = await idempotency.PurgeAsync(olderThan, 500, ct);
        }
        while (purged >= 500 && !ct.IsCancellationRequested);
    }

    private async Task RunPassAsync(string name, Func<CancellationToken, Task> pass, CancellationToken ct)
    {
        try { await pass(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Order worker {Pass} pass failed; the next pass still runs.", name); }
    }

    private async Task DispatchAsync(Command command, string what, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        using var scope = scopeFactory.CreateScope();
        try
        {
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
            if (!response.IsSuccess) logger.LogWarning("Order worker: {What} was refused.", what);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown, not a per-item failure.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order worker failed for {What}.", what);
        }
    }
}
