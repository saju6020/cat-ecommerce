using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-ORD-001 §12.2 -- the same shape and rules as HO-NTF-001's <c>NotificationWorker</c> and HO-IMP-001's <c>InventoryWorker</c>: a scope per poll,
/// <b>never throws out of <c>ExecuteAsync</c></b>, disabled in <c>Testing</c> (<c>Orders:Worker:Enabled=false</c>); tests call <see cref="OrderWorkCycle"/>
/// directly. A throwing poll is logged and the next one still runs (U-X1).
/// </summary>
public sealed class OrderWorker(IServiceScopeFactory scopeFactory, IOptions<OrdersOptions> options, ILogger<OrderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("OrderWorker is disabled (Orders:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OrderWorkCycle>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OrderWorker pass failed; will retry on the next poll.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, worker.IntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
