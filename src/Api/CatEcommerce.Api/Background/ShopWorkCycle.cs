using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-SHP-001 section 12.2 -- one pass: <c>GetDueStockAlertsQuery</c> (Pending alerts whose SKU is now
/// available, oldest first, at most 500) then <c>NotifyStockAlertCommand</c> per alert. The command
/// handler enqueues the message through the outbox before its own save (HO-NTF-001 ADR-057), so a
/// failure on one alert never loses or duplicates a message and never stops the pass. Scoped --
/// <see cref="ShopWorker"/> creates one scope per poll; each alert is dispatched in a fresh scope
/// from the injected <see cref="IServiceScopeFactory"/> (its own <c>DbContext</c>), exactly like
/// <c>NotificationWorkCycle</c> and <c>ClinicWorkCycle</c>. Callable by tests.
/// </summary>
public sealed class ShopWorkCycle(ILogger<ShopWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher)
{
    private static UserContext WorkerContext => new(ShopActors.Worker, Guid.Empty);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var due = await dispatcher.GetResultAsync(new GetDueStockAlertsQuery { UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });

        foreach (var alertId in due.Result ?? [])
        {
            if (ct.IsCancellationRequested) return;

            using var scope = scopeFactory.CreateScope();
            try
            {
                var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
                await scopedDispatcher.SendAsync(new NotifyStockAlertCommand
                { StockAlertId = alertId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown, not a per-item failure.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Shop worker pass failed for stock alert {StockAlertId}.", alertId);
            }
        }
    }
}
